using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.ViewModels.Workspace;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// タブ 1 枚のふるまい。文面を書く → 実行 → 結果ペイン、までを追う。
/// タブの開閉と行き来は <see cref="QueryEditorViewModelTests"/> にある。
/// </summary>
public class QueryDocumentViewModelTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public void ツリーから開くとエディタが空で出て実行先だけが決まる()
    {
        // 接続時に開いたのは shop。ツリーから開いた先のデータベースで上書きされる。
        var document = Open(ReadWriteSession());

        Assert.Equal("sales_db", document.TargetDatabase);
        Assert.Equal(string.Empty, document.Sql);

        // 打ち込むまでは変更の印を出さない（SSMS と同じ）。
        Assert.False(document.IsModified);
        Assert.Equal("SQLQuery1.sql", document.Title);
    }

    [Fact]
    public void 打ち込むと見出しに変更の印が付く()
    {
        var document = Open(ReadWriteSession());

        document.Sql = "SELECT 1";

        Assert.True(document.IsModified);
        Assert.Equal("SQLQuery1.sql*", document.Title);
    }

    [Fact]
    public void 開いただけでは実行しない()
    {
        var session = ReadWriteSession();
        var document = Open(session);

        Assert.Null(session.ExecutedSql);
        Assert.Empty(document.Tabs);
    }

    [Fact]
    public async Task 実行すると結果とメッセージのタブが並ぶ()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.FromMilliseconds(128));

        var document = Open(session);
        document.Sql = "SELECT 1";

        await document.RunCommand.ExecuteAsync(null);

        Assert.Equal(["結果 1", "メッセージ"], document.Tabs.Select(tab => tab.Title));
        Assert.True(document.SelectedTab!.IsGrid);
        Assert.Equal("1 行", document.Tabs[0].Badge);
        Assert.False(document.HasFailed);
        Assert.Contains("128 ms", document.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 結果セットの中身がグリッドの列と行になる()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);

        var document = Open(session);
        document.Sql = "SELECT 1";

        await document.RunCommand.ExecuteAsync(null);

        var grid = document.Tabs[0].ResultSet!;
        Assert.Equal(["region", "revenue"], grid.Columns.Select(column => column.Name));

        var cells = grid.Rows.Single().Cells;
        Assert.Equal("北米", cells[0].Text);
        Assert.False(cells[0].IsNull);

        // NULL は「NULL」という値の入ったセルと見分けが付くようにしておく。
        Assert.True(cells[1].IsNull);
        Assert.Equal("NULL", cells[1].Text);

        // 数値の列だけ右へ寄せる。
        Assert.False(grid.Columns[0].IsNumeric);
        Assert.True(grid.Columns[1].IsNumeric);
    }

    [Fact]
    public async Task 実行し直すと前の結果を持ち越さない()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);

        var document = Open(session);
        document.Sql = "SELECT 1";
        await document.RunCommand.ExecuteAsync(null);

        session.NextResult = new QueryResult([], 3, TimeSpan.Zero);
        await document.RunCommand.ExecuteAsync(null);

        Assert.Equal("メッセージ", Assert.Single(document.Tabs).Title);
    }

    [Fact]
    public async Task 行が返らないときはメッセージのタブが選ばれる()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([], 3, TimeSpan.Zero);

        var document = Open(session);
        document.Sql = "UPDATE dbo.orders SET status = 'paid'";

        await document.RunCommand.ExecuteAsync(null);

        Assert.Equal("メッセージ", document.SelectedTab!.Title);
        Assert.Contains("3 行処理されました", document.SelectedTab.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 失敗は理由をメッセージへ出す()
    {
        var session = ReadWriteSession();
        session.QueryFailure = new InvalidOperationException("オブジェクト名 'nope' が無効です。");

        var document = Open(session);
        document.Sql = "SELECT * FROM nope";

        await document.RunCommand.ExecuteAsync(null);

        Assert.True(document.HasFailed);
        Assert.Equal("メッセージ", document.SelectedTab!.Title);
        Assert.Contains("nope", document.SelectedTab.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 読み取り専用で開いた接続でも書き込みは止めない()
    {
        // 見本データの先頭は本番タグの読み取り専用接続。印を出すだけで、文面は素通しする
        //（止めるのはサーバー側の権限の仕事）。
        var session = new FakeDatabaseSession();
        Assert.True(session.Profile.IsReadOnly);

        var document = Open(session);
        document.Sql = "DELETE FROM dbo.orders";

        await document.RunCommand.ExecuteAsync(null);

        Assert.Equal("DELETE FROM dbo.orders", session.ExecutedSql);
        Assert.False(document.HasFailed);
    }

    [Fact]
    public async Task ツリーから文面付きで開くと即座に実行される()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenAndRunQuery(SalesDb, "SELECT TOP (1000) * FROM [dbo].[orders];");

        var document = editor.SelectedDocument!;

        // 実行は非同期に始まる。実行中を経て終わるまで待つ。
        while (document.IsRunning)
        {
            await Task.Yield();
        }

        Assert.Equal("sales_db", document.TargetDatabase);
        Assert.Equal("SELECT TOP (1000) * FROM [dbo].[orders];", document.Sql);
        Assert.Equal("SELECT TOP (1000) * FROM [dbo].[orders];", session.ExecutedSql);
        Assert.Equal(["結果 1", "メッセージ"], document.Tabs.Select(tab => tab.Title));
    }

    [Fact]
    public void 空の文面では実行ボタンが押せない()
    {
        var document = Open(ReadWriteSession());

        Assert.False(document.RunCommand.CanExecute(null));

        document.Sql = "SELECT 1";

        Assert.True(document.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task 打ち切った結果はその旨を添える()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([Truncated()], -1, TimeSpan.Zero);

        var document = Open(session);
        document.Sql = "SELECT 1";

        await document.RunCommand.ExecuteAsync(null);

        Assert.True(document.Tabs[0].ResultSet!.IsTruncated);
        Assert.Contains("打ち切り", document.Status, StringComparison.Ordinal);
        Assert.Contains("取得上限", document.Tabs[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void 整形すると文面が整う()
    {
        var document = Open(ReadWriteSession());
        document.Sql = "select a, b from dbo.orders";

        document.FormatCommand.Execute(null);

        Assert.Equal("SELECT\n    a,\n    b\nFROM dbo.orders", document.Sql);
    }

    [Fact]
    public void 文面が空のあいだは整形も実行もできない()
    {
        var document = Open(ReadWriteSession());

        Assert.False(document.FormatCommand.CanExecute(null));
        Assert.False(document.RunCommand.CanExecute(null));

        document.Sql = "SELECT 1";

        Assert.True(document.FormatCommand.CanExecute(null));
        Assert.True(document.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task 補完の口を渡していなければ候補は出ない()
    {
        var document = Open(ReadWriteSession());
        document.Sql = "SELECT * FROM ";

        var result = await document.CompleteAsync(document.Sql.Length);

        Assert.True(result.IsEmpty);
    }

    private static QueryResultSet OneRow() =>
        new(
            [
                new QueryColumn("region", "nvarchar", IsNumeric: false),
                new QueryColumn("revenue", "decimal", IsNumeric: true)
            ],
            [new string?[] { "北米", null }]);

    private static QueryResultSet Truncated() =>
        new(
            [new QueryColumn("n", "int", IsNumeric: true)],
            [new string?[] { "1" }],
            isTruncated: true);

    /// <summary>タブは必ずタブ帯から生まれるので、テストも同じ道で 1 枚だけ開く。</summary>
    private static QueryDocumentViewModel Open(FakeDatabaseSession session)
    {
        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);

        return editor.SelectedDocument!;
    }

    private static FakeDatabaseSession ReadWriteSession()
    {
        var profile = SeedConnections.Create().First(candidate => candidate.AccessMode == AccessMode.ReadWrite);

        return new FakeDatabaseSession(profile);
    }
}
