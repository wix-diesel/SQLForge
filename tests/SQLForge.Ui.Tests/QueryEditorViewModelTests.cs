using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.ViewModels.Workspace;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 作業領域のふるまい。ツリーから開く → 文面を書く → 実行 → 結果ペイン、までを追う。
/// </summary>
public class QueryEditorViewModelTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public void ツリーから開くとエディタが空で出て実行先だけが決まる()
    {
        // 接続時に開いたのは shop。ツリーから開いた先のデータベースで上書きされる。
        var session = ReadWriteSession();
        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());

        Assert.False(editor.IsOpen);

        editor.OpenNewQuery(SalesDb);

        Assert.True(editor.IsOpen);
        Assert.Equal("sales_db", editor.TargetDatabase);
        Assert.Equal(string.Empty, editor.Sql);
    }

    [Fact]
    public void 開いただけでは実行しない()
    {
        var session = ReadWriteSession();
        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());

        editor.OpenNewQuery(SalesDb);

        Assert.Null(session.ExecutedSql);
        Assert.Empty(editor.Tabs);
    }

    [Fact]
    public async Task 開き直すと前の結果を持ち越さない()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "SELECT 1";
        await editor.RunCommand.ExecuteAsync(null);

        editor.OpenNewQuery(SalesDb);

        Assert.Equal(string.Empty, editor.Sql);
        Assert.Empty(editor.Tabs);
        Assert.Null(editor.SelectedTab);
        Assert.Equal(string.Empty, editor.Status);
    }

    [Fact]
    public async Task 実行中に開き直すと後から返ってきた結果は出さない()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);
        session.QueryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "SELECT 1";

        var running = editor.RunCommand.ExecuteAsync(null);

        // 結果が返る前に、ツリーから別のクエリを開き直す。
        editor.OpenNewQuery(SalesDb);
        session.QueryGate.SetResult();
        await running;

        // 開き直した先はもう別のクエリ。そこへ前の実行の結果を出すと嘘になる。
        Assert.Empty(editor.Tabs);
        Assert.Null(editor.SelectedTab);
        Assert.Equal(string.Empty, editor.Status);
        Assert.False(editor.HasFailed);
    }

    [Fact]
    public async Task 実行すると結果とメッセージのタブが並ぶ()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.FromMilliseconds(128));

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "SELECT 1";

        await editor.RunCommand.ExecuteAsync(null);

        Assert.Equal(["結果 1", "メッセージ"], editor.Tabs.Select(tab => tab.Title));
        Assert.True(editor.SelectedTab!.IsGrid);
        Assert.Equal("1 行", editor.Tabs[0].Badge);
        Assert.False(editor.HasFailed);
        Assert.Contains("128 ms", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 結果セットの中身がグリッドの列と行になる()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "SELECT 1";

        await editor.RunCommand.ExecuteAsync(null);

        var grid = editor.Tabs[0].ResultSet!;
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
    public async Task 行が返らないときはメッセージのタブが選ばれる()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([], 3, TimeSpan.Zero);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "UPDATE dbo.orders SET status = 'paid'";

        await editor.RunCommand.ExecuteAsync(null);

        Assert.Equal("メッセージ", editor.SelectedTab!.Title);
        Assert.Contains("3 行処理されました", editor.SelectedTab.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 失敗は理由をメッセージへ出す()
    {
        var session = ReadWriteSession();
        session.QueryFailure = new InvalidOperationException("オブジェクト名 'nope' が無効です。");

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "SELECT * FROM nope";

        await editor.RunCommand.ExecuteAsync(null);

        Assert.True(editor.HasFailed);
        Assert.Equal("メッセージ", editor.SelectedTab!.Title);
        Assert.Contains("nope", editor.SelectedTab.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 読み取り専用で開いた接続でも書き込みは止めない()
    {
        // 見本データの先頭は本番タグの読み取り専用接続。印を出すだけで、文面は素通しする
        //（止めるのはサーバー側の権限の仕事）。
        var session = new FakeDatabaseSession();
        Assert.True(session.Profile.IsReadOnly);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "DELETE FROM dbo.orders";

        await editor.RunCommand.ExecuteAsync(null);

        Assert.Equal("DELETE FROM dbo.orders", session.ExecutedSql);
        Assert.False(editor.HasFailed);
    }

    [Fact]
    public async Task ツリーから文面付きで開くと開くと同時に実行される()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenAndRunQuery(SalesDb, "SELECT TOP (1000) * FROM [dbo].[orders];");

        // 実行は非同期に始まる。実行中を経て終わるまで待つ。
        while (editor.IsRunning)
        {
            await Task.Yield();
        }

        Assert.True(editor.IsOpen);
        Assert.Equal("sales_db", editor.TargetDatabase);
        Assert.Equal("SELECT TOP (1000) * FROM [dbo].[orders];", editor.Sql);
        Assert.Equal("SELECT TOP (1000) * FROM [dbo].[orders];", session.ExecutedSql);
        Assert.Equal(["結果 1", "メッセージ"], editor.Tabs.Select(tab => tab.Title));
    }

    [Fact]
    public void 空の文面では実行ボタンが押せない()
    {
        var editor = new QueryEditorViewModel(ReadWriteSession(), new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);

        Assert.False(editor.RunCommand.CanExecute(null));

        editor.Sql = "SELECT 1";

        Assert.True(editor.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task 打ち切った結果はその旨を添える()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([Truncated()], -1, TimeSpan.Zero);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);
        editor.Sql = "SELECT 1";

        await editor.RunCommand.ExecuteAsync(null);

        Assert.True(editor.Tabs[0].ResultSet!.IsTruncated);
        Assert.Contains("打ち切り", editor.Status, StringComparison.Ordinal);
        Assert.Contains("取得上限", editor.Tabs[1].Text, StringComparison.Ordinal);
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

    private static FakeDatabaseSession ReadWriteSession()
    {
        var profile = SeedConnections.Create().First(candidate => candidate.AccessMode == AccessMode.ReadWrite);

        return new FakeDatabaseSession(profile);
    }
}
