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

    private static IReadOnlyList<EditableColumn> Columns() =>
    [
        new EditableColumn("id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false),
        new EditableColumn(
            "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false, IsText: true)
    ];

    /// <summary>id は書き換えられる（主キーの打ち直しは SSMS でもできる）。</summary>
    private static IReadOnlyList<EditableColumn> EditableKeyColumns() =>
    [
        new EditableColumn("id", "int", IsNullable: false, IsKey: true, IsReadOnly: false, IsNumeric: true, IsText: false)
    ];

    private static IReadOnlyList<EditableColumn> ReadOnlyColumns() =>
    [
        new EditableColumn("id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false),
        new EditableColumn(
            "created_at", "datetime2(7)", IsNullable: false, IsKey: false, IsReadOnly: true, IsNumeric: false, IsText: false)
    ];
}
