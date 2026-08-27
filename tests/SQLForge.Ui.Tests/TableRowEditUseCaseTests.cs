using SQLForge.Application.Editing;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 編集グリッドの行の追加と削除（SSMS の編集グリッドと同じ入口）のユースケース。
///
/// 見るのは「何を送らないか」。触っていない列を送ると既定値が効かなくなり、
/// 条件を間違えると画面に出ていない行まで消える。
/// </summary>
public class TableRowEditUseCaseTests
{
    private static readonly DatabaseName ShopDb = new("shop");
    private static readonly SchemaName Dbo = new("dbo");

    [Fact]
    public async Task 打ち込まれた列だけを足す()
    {
        // 触っていない列を NULL として送ると、サーバーの既定値が効かなくなる。
        var session = new FakeDatabaseSession();

        await Insert(session, new TableCellValue("customer", "tanaka"));

        var insert = Assert.IsType<TableRowInsert>(session.LastInsert);
        Assert.Equal("shop.dbo.orders", session.InsertedTable);
        Assert.Equal([("customer", "tanaka")], insert.Values.Select(value => (value.Column, value.Value)));
    }

    [Fact]
    public async Task 何も打ち込まれていない行は足さない()
    {
        var session = new FakeDatabaseSession();

        await Assert.ThrowsAsync<TableEditRejectedException>(() => Insert(session));

        Assert.Null(session.LastInsert);
    }

    [Fact]
    public async Task サーバーが決める列には値を置けない()
    {
        // id は IDENTITY。打ち込めないので、文面にも出さない。
        var session = new FakeDatabaseSession();

        var rejected = await Assert.ThrowsAsync<TableEditRejectedException>(
            () => Insert(session, new TableCellValue("id", "8")));

        Assert.Contains("id", rejected.Message, StringComparison.Ordinal);
        Assert.Null(session.LastInsert);
    }

    [Fact]
    public async Task NULLを許さない列にNULLは置けない()
    {
        var session = new FakeDatabaseSession();

        await Assert.ThrowsAsync<TableEditRejectedException>(
            () => Insert(session, new TableCellValue("customer", null)));

        Assert.Null(session.LastInsert);
    }

    [Fact]
    public async Task 足したあとの行をそのまま返す()
    {
        // IDENTITY や既定値でサーバーが決めた値を画面へ写すために使う。
        var session = new FakeDatabaseSession { InsertedRow = ["8", "tanaka", null] };

        var inserted = await Insert(session, new TableCellValue("customer", "tanaka"));

        Assert.Equal(["8", "tanaka", null], inserted);
    }

    [Fact]
    public void 同じ列に2つの値は置けない()
    {
        // 並びに同じ名前が 2 度出ると、INSERT の文面が壊れる。
        Assert.Throws<ArgumentException>(() =>
            new TableRowInsert([new TableCellValue("customer", "tanaka"), new TableCellValue("customer", "sato")]));
    }

    [Fact]
    public async Task 主キーの列だけを条件にして消す()
    {
        var session = new FakeDatabaseSession();

        await Delete(session);

        var delete = Assert.IsType<TableRowDelete>(session.LastDelete);
        Assert.Equal("shop.dbo.orders", session.DeletedTable);
        Assert.Equal([("id", "7")], delete.Criteria.Select(criterion => (criterion.Column, criterion.Value)));
    }

    [Fact]
    public async Task 行を特定できない列しか無ければ消さない()
    {
        var session = new FakeDatabaseSession();

        var request = new DeleteTableRowRequest(
            ShopDb, Dbo, "notes", [Column("body", isKey: false)], ["memo"]);

        await Assert.ThrowsAsync<TableEditRejectedException>(
            () => new DeleteTableRowUseCase().ExecuteAsync(session, request));

        Assert.Null(session.LastDelete);
    }

    [Fact]
    public async Task 一行も当たらなければ読み直しを促す()
    {
        var session = new FakeDatabaseSession { DeletedRows = 0 };

        var rejected = await Assert.ThrowsAsync<TableEditRejectedException>(() => Delete(session));

        Assert.Contains("読み直して", rejected.Message, StringComparison.Ordinal);
    }

    /// <summary>id（IDENTITY・主キー）・customer（NOT NULL）・status（NULL 可）の 3 列。</summary>
    private static IReadOnlyList<EditableColumn> Columns() =>
    [
        new EditableColumn(
            "id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false,
            IsIdentity: true),
        Column("customer", isKey: false),
        new EditableColumn(
            "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false, IsText: true)
    ];

    private static EditableColumn Column(string name, bool isKey) =>
        new(name, "nvarchar(50)", IsNullable: false, isKey, IsReadOnly: false, IsNumeric: false, IsText: true);

    private static Task<IReadOnlyList<string?>?> Insert(FakeDatabaseSession session, params TableCellValue[] values) =>
        new InsertTableRowUseCase().ExecuteAsync(
            session,
            new InsertTableRowRequest(ShopDb, Dbo, "orders", Columns(), values));

    private static Task<int> Delete(FakeDatabaseSession session) =>
        new DeleteTableRowUseCase().ExecuteAsync(
            session,
            new DeleteTableRowRequest(ShopDb, Dbo, "orders", Columns(), ["7", "tanaka", "paid"]));
}
