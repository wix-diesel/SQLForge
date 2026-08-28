using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.ViewModels.Workspace;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 作業領域のタブ帯。開く・切り替える・閉じるまでを追う（SSMS のクエリ ウィンドウと同じ扱い）。
/// タブ 1 枚の中のふるまいは <see cref="QueryDocumentViewModelTests"/> にある。
/// </summary>
public class QueryEditorViewModelTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");
    private static readonly DatabaseName ShopDb = new("shop_db");

    [Fact]
    public void 開くまではタブが1枚もない()
    {
        var editor = NewEditor();

        Assert.False(editor.IsOpen);
        Assert.Empty(editor.Documents);
        Assert.Null(editor.SelectedDocument);
    }

    [Fact]
    public void ツリーから開くたびにタブが増える()
    {
        var editor = NewEditor();

        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(ShopDb);

        Assert.True(editor.IsOpen);
        Assert.Equal(2, editor.Documents.Count);

        // 開いた先が前に出る。前のタブは畳まれずそのまま残る。
        Assert.Same(editor.Documents[1], editor.SelectedDocument);
    }

    [Fact]
    public void タブの見出しはSSMSと同じ連番で付く()
    {
        var editor = NewEditor();

        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);

        Assert.Equal(["SQLQuery1.sql", "SQLQuery2.sql"], editor.Documents.Select(document => document.Name));
    }

    [Fact]
    public void 閉じたタブの番号は使い回さない()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);

        editor.Documents[1].CloseCommand.Execute(null);
        editor.NewDocumentCommand.Execute(null);

        Assert.Equal(["SQLQuery1.sql", "SQLQuery3.sql"], editor.Documents.Select(document => document.Name));
    }

    [Fact]
    public void タブごとに文面と実行先を別々に持つ()
    {
        var editor = NewEditor();

        editor.OpenNewQuery(SalesDb);
        editor.SelectedDocument!.Sql = "SELECT 1";

        editor.OpenNewQuery(ShopDb);
        editor.SelectedDocument!.Sql = "SELECT 2";

        Assert.Equal("SELECT 1", editor.Documents[0].Sql);
        Assert.Equal("sales_db", editor.Documents[0].TargetDatabase);
        Assert.Equal("SELECT 2", editor.Documents[1].Sql);
        Assert.Equal("shop_db", editor.Documents[1].TargetDatabase);
    }

    [Fact]
    public void 新しいタブは今のタブと同じ実行先で開く()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(ShopDb);

        editor.NewDocumentCommand.Execute(null);

        Assert.Equal("shop_db", editor.SelectedDocument!.TargetDatabase);
    }

    [Fact]
    public void タブが1枚も無いときの新しいタブは接続時のデータベースを実行先にする()
    {
        // 見本データの接続が開いているのは shop。
        var session = ReadWriteSession();
        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());

        editor.NewDocumentCommand.Execute(null);

        Assert.Equal(session.Profile.Target.Database, editor.SelectedDocument!.TargetDatabase);
    }

    [Fact]
    public void タブを閉じると残りが詰まり最後の1枚で作業領域が畳まれる()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(ShopDb);

        editor.CloseSelectedCommand.Execute(null);

        Assert.Single(editor.Documents);
        Assert.True(editor.IsOpen);

        editor.CloseSelectedCommand.Execute(null);

        Assert.Empty(editor.Documents);
        Assert.Null(editor.SelectedDocument);
        Assert.False(editor.IsOpen);
    }

    [Fact]
    public void 今のタブを閉じると直前に見ていたタブが前に出る()
    {
        // SSMS と同じで、閉じた先は「隣」ではなく「直前に見ていたもの」。
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);   // SQLQuery1
        editor.OpenNewQuery(SalesDb);   // SQLQuery2
        editor.OpenNewQuery(SalesDb);   // SQLQuery3

        editor.SelectedDocument = editor.Documents[0];
        editor.SelectedDocument = editor.Documents[2];

        editor.CloseSelectedCommand.Execute(null);

        Assert.Same(editor.Documents[0], editor.SelectedDocument);
    }

    [Fact]
    public void 前に出ていないタブを閉じても今見ているタブは変わらない()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);

        var selected = editor.SelectedDocument!;
        editor.Documents[0].CloseCommand.Execute(null);

        Assert.Same(selected, editor.SelectedDocument);
    }

    [Fact]
    public void これ以外を閉じると押したタブだけが残る()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);

        var kept = editor.Documents[1];
        kept.CloseOthersCommand.Execute(null);

        Assert.Same(kept, Assert.Single(editor.Documents));
        Assert.Same(kept, editor.SelectedDocument);
    }

    [Fact]
    public void すべて閉じると作業領域が畳まれる()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);

        editor.Documents[0].CloseAllCommand.Execute(null);

        Assert.Empty(editor.Documents);
        Assert.False(editor.IsOpen);
    }

    [Fact]
    public void 次と前でタブを行き来する()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);
        editor.OpenNewQuery(SalesDb);

        editor.SelectedDocument = editor.Documents[0];

        editor.NextDocumentCommand.Execute(null);
        Assert.Same(editor.Documents[1], editor.SelectedDocument);

        editor.PreviousDocumentCommand.Execute(null);
        Assert.Same(editor.Documents[0], editor.SelectedDocument);

        // 端まで来たら反対の端へ回る。
        editor.PreviousDocumentCommand.Execute(null);
        Assert.Same(editor.Documents[2], editor.SelectedDocument);

        editor.NextDocumentCommand.Execute(null);
        Assert.Same(editor.Documents[0], editor.SelectedDocument);
    }

    [Fact]
    public void タブが1枚のときは行き来する先がない()
    {
        var editor = NewEditor();

        Assert.False(editor.NextDocumentCommand.CanExecute(null));
        Assert.False(editor.CloseSelectedCommand.CanExecute(null));

        editor.OpenNewQuery(SalesDb);

        Assert.False(editor.NextDocumentCommand.CanExecute(null));
        Assert.True(editor.CloseSelectedCommand.CanExecute(null));

        editor.OpenNewQuery(SalesDb);

        Assert.True(editor.NextDocumentCommand.CanExecute(null));
    }

    [Fact]
    public async Task 実行中のタブを閉じると実行を取り消す()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([], -1, TimeSpan.Zero);
        session.QueryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);

        var document = editor.SelectedDocument!;
        document.Sql = "SELECT 1";
        var running = document.RunCommand.ExecuteAsync(null);

        document.CloseCommand.Execute(null);

        // 取り消しが効かなかったときに、待ち続けずテストとして落ちるようにしておく。
        session.QueryGate.SetResult();
        await running;

        Assert.Empty(editor.Documents);
        Assert.False(document.IsRunning);

        // 走らせたまま放り出さず、取り消しとして終わっていること。
        Assert.True(document.HasFailed);
        Assert.Equal("実行を取り消しました。", document.SelectedTab!.Text);
    }

    [Fact]
    public async Task 実行中に別のタブを開いても結果は元のタブに出る()
    {
        var session = ReadWriteSession();
        session.NextResult = new QueryResult([OneRow()], -1, TimeSpan.Zero);
        session.QueryGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var editor = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        editor.OpenNewQuery(SalesDb);

        var first = editor.SelectedDocument!;
        first.Sql = "SELECT 1";
        var running = first.RunCommand.ExecuteAsync(null);

        // 待っている間に、ツリーから別のクエリを開く。
        editor.OpenNewQuery(SalesDb);
        session.QueryGate.SetResult();
        await running;

        // 結果は走らせたタブのもの。前に出ているタブは巻き添えにしない。
        Assert.Equal(["結果 1", "メッセージ"], first.Tabs.Select(tab => tab.Title));
        Assert.Empty(editor.SelectedDocument!.Tabs);
    }

    [Fact]
    public void ツリーから文面付きで開くとその文面のタブが増える()
    {
        var editor = NewEditor();
        editor.OpenNewQuery(SalesDb);

        editor.OpenAndRunQuery(SalesDb, "SELECT TOP (1000) * FROM [dbo].[orders];");

        Assert.Equal(2, editor.Documents.Count);
        Assert.Equal("SELECT TOP (1000) * FROM [dbo].[orders];", editor.SelectedDocument!.Sql);
    }

    [Fact]
    public void 空の文面で開こうとすると例外になる()
    {
        var editor = NewEditor();

        Assert.Throws<ArgumentException>(() => editor.OpenAndRunQuery(SalesDb, "   "));
        Assert.Empty(editor.Documents);
    }

    private static QueryResultSet OneRow() =>
        new([new QueryColumn("n", "int", IsNumeric: true)], [new string?[] { "1" }]);

    private static QueryEditorViewModel NewEditor() =>
        new(ReadWriteSession(), new ExecuteQueryUseCase());

    private static FakeDatabaseSession ReadWriteSession()
    {
        var profile = SeedConnections.Create().First(candidate => candidate.AccessMode == AccessMode.ReadWrite);

        return new FakeDatabaseSession(profile);
    }
}
