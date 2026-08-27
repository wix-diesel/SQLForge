using SQLForge.Application.Editing;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 編集グリッドが投げる文面。
///
/// 値はすべてパラメータへ逃がし、識別子だけを角括弧で囲む。NULL の条件を
/// <c>= NULL</c> で書くとどの行にも当たらないので、そこは別扱いになっているかを見る。
/// </summary>
public class SqlServerEditStatementsTests
{
    private static readonly SchemaName Dbo = new("dbo");

    [Fact]
    public void 先頭N行は列名を並べて読む()
    {
        var statement = SqlServerEditStatements.TopRows(Dbo, "orders", Columns(), maxRows: 100);

        Assert.Equal("SELECT TOP (@p0) [id], [status] FROM [dbo].[orders];", statement.Text);
        Assert.Equal([100], statement.Parameters);
    }

    [Fact]
    public void 識別子は角括弧で囲んで閉じられないようにする()
    {
        var statement = SqlServerEditStatements.TopRows(new SchemaName("dbo]--"), "orders", Columns(), maxRows: 100);

        Assert.Contains("[dbo]]--].[orders]", statement.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void 更新は主キーを条件にして値をパラメータで渡す()
    {
        var update = new TableCellUpdate("status", "shipped", [new RowCriterion("id", "7")]);

        var statement = SqlServerEditStatements.CellUpdate(Dbo, "orders", Columns(), update);

        Assert.Equal("UPDATE [dbo].[orders] SET [status] = @p0 WHERE [id] = @p1;", statement.Text);
        Assert.Equal(["shipped", 7L], statement.Parameters);
    }

    [Fact]
    public void 変更前がNULLの条件はISNULLで比べる()
    {
        // = NULL は常に不定になり、どの行にも当たらない。
        var update = new TableCellUpdate("status", "shipped", [new RowCriterion("id", "7"), new RowCriterion("status", null)]);

        var statement = SqlServerEditStatements.CellUpdate(Dbo, "orders", Columns(), update);

        Assert.EndsWith("WHERE [id] = @p1 AND [status] IS NULL;", statement.Text, StringComparison.Ordinal);
        Assert.Equal(2, statement.Parameters.Count);
    }

    [Fact]
    public void NULLへの書き換えはパラメータもNULLで渡す()
    {
        var update = new TableCellUpdate("status", null, [new RowCriterion("id", "7")]);

        var statement = SqlServerEditStatements.CellUpdate(Dbo, "orders", Columns(), update);

        Assert.Null(statement.Parameters[0]);
    }

    [Fact]
    public void 書き換えられない列は文面を組む前に弾く()
    {
        var update = new TableCellUpdate("created_at", "2026-08-26", [new RowCriterion("id", "7")]);

        Assert.Throws<TableEditRejectedException>(
            () => SqlServerEditStatements.CellUpdate(Dbo, "orders", ReadOnlyColumns(), update));
    }

    [Fact]
    public void 型に合わない値はサーバーへ送る前に弾く()
    {
        var update = new TableCellUpdate("id", "七番", [new RowCriterion("id", "7")]);

        var rejected = Assert.Throws<TableEditRejectedException>(
            () => SqlServerEditStatements.CellUpdate(Dbo, "orders", EditableKeyColumns(), update));

        Assert.Contains("int", rejected.Message, StringComparison.Ordinal);
    }

    /// <summary>id（IDENTITY・主キー）と status（nvarchar）。</summary>
    private static IReadOnlyList<EditableColumn> Columns() =>
    [
        new EditableColumn(
            "id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false,
            IsIdentity: true),
        new EditableColumn(
            "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false, IsText: true)
    ];

    /// <summary>id は書き換えられる（主キーの打ち直しは SSMS でもできる）。</summary>
    private static IReadOnlyList<EditableColumn> EditableKeyColumns() =>
    [
        new EditableColumn("id", "int", IsNullable: false, IsKey: true, IsReadOnly: false, IsNumeric: true, IsText: false)
    ];

    [Fact]
    public void 行の追加は打ち込まれた列だけを並べる()
    {
        // 触っていない列を NULL として並べると、サーバーの既定値が効かなくなる。
        var insert = new TableRowInsert([new TableCellValue("status", "shipped")]);

        var statement = SqlServerEditStatements.RowInsert(Dbo, "orders", Columns(), insert);

        Assert.StartsWith(
            "INSERT INTO [dbo].[orders] ([status]) VALUES (@p0);",
            statement.Text,
            StringComparison.Ordinal);
        Assert.Equal(["shipped"], statement.Parameters);
    }

    [Fact]
    public void 採番される鍵はSCOPEIDENTITYで読み直す()
    {
        // IDENTITY の値は足してみるまで分からない。同じ文面の中で読み直して画面へ写す。
        var insert = new TableRowInsert([new TableCellValue("status", "shipped")]);

        var statement = SqlServerEditStatements.RowInsert(Dbo, "orders", Columns(), insert);

        Assert.EndsWith(
            " SELECT TOP (1) [id], [status] FROM [dbo].[orders] WHERE [id] = SCOPE_IDENTITY();",
            statement.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void 打ち込まれた鍵はその値で読み直す()
    {
        var insert = new TableRowInsert(
            [new TableCellValue("code", "A-1"), new TableCellValue("status", "shipped")]);

        var statement = SqlServerEditStatements.RowInsert(Dbo, "orders", KeyedColumns(), insert);

        Assert.EndsWith(
            " SELECT TOP (1) [code], [status] FROM [dbo].[orders] WHERE [code] = @p2;",
            statement.Text,
            StringComparison.Ordinal);
        Assert.Equal(["A-1", "shipped", "A-1"], statement.Parameters);
    }

    [Fact]
    public void 何が入るか分からない鍵では読み直さない()
    {
        // 既定値で決まる主キー（newid() など）は、足したあとに当てにいけない。
        var insert = new TableRowInsert([new TableCellValue("status", "shipped")]);

        var statement = SqlServerEditStatements.RowInsert(Dbo, "orders", KeyedColumns(), insert);

        Assert.EndsWith("VALUES (@p0);", statement.Text, StringComparison.Ordinal);
        Assert.Single(statement.Parameters);
    }

    [Fact]
    public void 値を指定できない列は文面を組む前に弾く()
    {
        var insert = new TableRowInsert([new TableCellValue("id", "8")]);

        Assert.Throws<TableEditRejectedException>(
            () => SqlServerEditStatements.RowInsert(Dbo, "orders", Columns(), insert));
    }

    [Fact]
    public void 削除は主キーを条件にして値をパラメータで渡す()
    {
        var delete = new TableRowDelete([new RowCriterion("id", "7")]);

        var statement = SqlServerEditStatements.RowDelete(Dbo, "orders", Columns(), delete);

        Assert.Equal("DELETE FROM [dbo].[orders] WHERE [id] = @p0;", statement.Text);
        Assert.Equal([7L], statement.Parameters);
    }

    [Fact]
    public void 削除でも変更前がNULLの条件はISNULLで比べる()
    {
        var delete = new TableRowDelete([new RowCriterion("id", "7"), new RowCriterion("status", null)]);

        var statement = SqlServerEditStatements.RowDelete(Dbo, "orders", Columns(), delete);

        Assert.EndsWith("WHERE [id] = @p0 AND [status] IS NULL;", statement.Text, StringComparison.Ordinal);
        Assert.Single(statement.Parameters);
    }

    /// <summary>採番されない主キー（code）を持つテーブル。</summary>
    private static IReadOnlyList<EditableColumn> KeyedColumns() =>
    [
        new EditableColumn(
            "code", "nvarchar(10)", IsNullable: false, IsKey: true, IsReadOnly: false, IsNumeric: false, IsText: true),
        new EditableColumn(
            "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false, IsText: true)
    ];

    private static IReadOnlyList<EditableColumn> ReadOnlyColumns() =>
    [
        new EditableColumn("id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false),
        new EditableColumn(
            "created_at", "datetime2(7)", IsNullable: false, IsKey: false, IsReadOnly: true, IsNumeric: false, IsText: false)
    ];
}
