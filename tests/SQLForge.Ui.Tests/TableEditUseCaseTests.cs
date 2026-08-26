using SQLForge.Application.Editing;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 先頭 N 行の編集（SSMS の「上位 200 行の編集」にあたる入口）のユースケース。
///
/// 見るのは「どの行を書き換えるか」の決め方。編集グリッドは 1 セットずつ UPDATE を投げるので、
/// 条件の組み方を間違えると、画面に出ていない行まで書き換わる。
/// </summary>
public class TableEditUseCaseTests
{
    private static readonly DatabaseName ShopDb = new("shop");
    private static readonly SchemaName Dbo = new("dbo");

    [Fact]
    public async Task 既定では先頭100行を読む()
    {
        var session = new FakeDatabaseSession();

        await new EditTableRowsUseCase().ExecuteAsync(session, ShopDb, Dbo, "orders");

        Assert.Equal(100, session.EditableMaxRows);
        Assert.Equal(1, session.EditableRowsCallCount);
    }

    [Fact]
    public async Task 主キーの列だけを条件にして更新する()
    {
        var session = new FakeDatabaseSession();

        await Update(session, ordinal: 2, value: "shipped");

        var update = Assert.IsType<TableCellUpdate>(session.LastUpdate);
        Assert.Equal("shop.dbo.orders", session.UpdatedTable);
        Assert.Equal("status", update.Column);
        Assert.Equal("shipped", update.Value);
        Assert.Equal(["id"], update.Criteria.Select(criterion => criterion.Column));
    }

    [Fact]
    public async Task 条件には変更前の値を使う()
    {
        // 画面に出ている新しい値で条件を組むと、書き換えたあとの行が見つからなくなる。
        var session = new FakeDatabaseSession();

        await Update(session, ordinal: 1, value: "sato");

        Assert.Equal("7", session.LastUpdate!.Criteria.Single().Value);
    }

    [Fact]
    public async Task IDENTITYや計算列は書き換えない()
    {
        var session = new FakeDatabaseSession();

        var rejected = await Assert.ThrowsAsync<TableEditRejectedException>(
            () => Update(session, ordinal: 0, value: "8"));

        Assert.Contains("id", rejected.Message, StringComparison.Ordinal);
        Assert.Null(session.LastUpdate);
    }

    [Fact]
    public async Task NULLを許さない列にNULLは入れない()
    {
        var session = new FakeDatabaseSession();

        await Assert.ThrowsAsync<TableEditRejectedException>(() => Update(session, ordinal: 1, value: null));

        Assert.Null(session.LastUpdate);
    }

    [Fact]
    public async Task 行を特定できない列しか無ければ更新しない()
    {
        // 鍵になる列が 1 つも無いテーブル。どの行を直すのか決められないので投げない。
        var session = new FakeDatabaseSession();

        var request = new TableCellEditRequest(
            ShopDb,
            Dbo,
            "notes",
            [Column("body", isKey: false)],
            ["memo"],
            Ordinal: 0,
            NewValue: "書き直し");

        await Assert.ThrowsAsync<TableEditRejectedException>(
            () => new UpdateTableCellUseCase().ExecuteAsync(session, request));

        Assert.Null(session.LastUpdate);
    }

    [Fact]
    public async Task 一行も当たらなければ読み直しを促す()
    {
        var session = new FakeDatabaseSession { UpdatedRows = 0 };

        var rejected = await Assert.ThrowsAsync<TableEditRejectedException>(
            () => Update(session, ordinal: 2, value: "shipped"));

        Assert.Contains("読み直して", rejected.Message, StringComparison.Ordinal);
    }

    /// <summary>id（IDENTITY・主キー）・customer（NOT NULL）・status（NULL 可）の 3 列。</summary>
    private static IReadOnlyList<EditableColumn> Columns() =>
    [
        new EditableColumn("id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false),
        Column("customer", isKey: false),
        new EditableColumn(
            "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false, IsText: true)
    ];

    private static EditableColumn Column(string name, bool isKey) =>
        new(name, "nvarchar(50)", IsNullable: false, isKey, IsReadOnly: false, IsNumeric: false, IsText: true);

    private static Task<int> Update(FakeDatabaseSession session, int ordinal, string? value) =>
        new UpdateTableCellUseCase().ExecuteAsync(
            session,
            new TableCellEditRequest(ShopDb, Dbo, "orders", Columns(), ["7", "tanaka", "paid"], ordinal, value));
}
