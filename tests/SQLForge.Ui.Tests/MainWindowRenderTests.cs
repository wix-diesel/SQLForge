using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Application.Catalog;
using SQLForge.Application.Editing;
using SQLForge.Application.Query;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Query;
using SQLForge.Domain.Security;
using SQLForge.Ui.Composition;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.ViewModels.Workspace;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 接続後の画面が実際に組み上がって描けること。
/// ツリーのコントロールテーマやリソース参照の取りこぼしは、ここで初めて表に出る。
/// </summary>
public class MainWindowRenderTests
{
    [AvaloniaFact]
    public void メインウィンドウが描画できる()
    {
        var window = CreateWindow(out _);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
    }

    [AvaloniaFact]
    public void ツリーにデータベースとテーブルの行が並ぶ()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        WaitFor(() => viewModel.Explorer.Roots.FirstOrDefault()?.Children.Count > 0);

        var databases = viewModel.Explorer.Roots[0].Children[0];
        WaitFor(() => databases.Children.OfType<DatabaseNode>().Any());

        // 「データベース → sales_db → スキーマ → dbo → テーブル」と、画面と同じ順に開いていく。
        var schemas = Expand<SchemaNode>(
            databases.Children.OfType<DatabaseNode>().First(node => node.Title == "sales_db"),
            "スキーマ");
        Expand<TableNode>(schemas.First(node => node.Title == "dbo"), "テーブル");

        Dispatcher.UIThread.RunJobs();

        // ツリーが実際に行を作っているかは、描かれた TreeViewItem の見出しから確かめる。
        var titles = window.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .Select(item => (item.DataContext as ObjectExplorerNode)?.Title)
            .ToList();

        Assert.Contains("sales_db", titles);
        Assert.Contains("dbo", titles);
        Assert.Contains("orders", titles);
    }

    [AvaloniaFact]
    public void ツリーにセキュリティとユーザーの行が並ぶ()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        WaitFor(() => viewModel.Explorer.Roots.FirstOrDefault()?.Children.Count > 0);

        var databases = viewModel.Explorer.Roots[0].Children[0];
        WaitFor(() => databases.Children.OfType<DatabaseNode>().Any());

        // 「データベース → sales_db → セキュリティ → ユーザー」と、画面と同じ順に開いていく。
        var database = databases.Children.OfType<DatabaseNode>().First();
        database.IsExpanded = true;
        WaitFor(() => database.Children.Any(node => node.Title == "セキュリティ"));

        var security = database.Children.First(node => node.Title == "セキュリティ");
        security.IsExpanded = true;
        WaitFor(() => security.Children.OfType<DatabaseUsersNode>().Any());

        var users = security.Children.OfType<DatabaseUsersNode>().First();
        users.IsExpanded = true;
        WaitFor(() => users.Children.OfType<DatabaseUserNode>().Any());

        Dispatcher.UIThread.RunJobs();

        var titles = window.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .Select(item => (item.DataContext as ObjectExplorerNode)?.Title)
            .ToList();

        Assert.Contains("セキュリティ", titles);
        Assert.Contains("ユーザー", titles);
        Assert.Contains("app_user", titles);
    }

    [AvaloniaFact]
    public void テーブルのクエリを実行すると作業領域にエディタが出る()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        var table = FindOrders(viewModel);
        var pane = window.GetVisualDescendants().OfType<QueryWorkspacePane>().Single();

        // 作業領域は右クリックで開くまで畳んである。
        Assert.False(viewModel.Query.IsOpen);
        Assert.False(pane.IsVisible);

        table.OpenQueryCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.Query.IsOpen);
        Assert.True(pane.IsVisible);

        // 開くのは空のエディタ。決まるのは実行先のデータベースだけ。
        Assert.Equal("sales_db", viewModel.Query.TargetDatabase);
        Assert.Equal(string.Empty, viewModel.Query.Sql);
    }

    [AvaloniaFact]
    public void 実行した結果がグリッドの行として描かれる()
    {
        var session = NewSession();
        session.NextResult = new QueryResult(
            [
                new QueryResultSet(
                    [new QueryColumn("region", "nvarchar", IsNumeric: false)],
                    [new string?[] { "北米" }])
            ],
            -1,
            TimeSpan.FromMilliseconds(12));

        var viewModel = NewViewModel(session);
        var window = new MainWindow { DataContext = viewModel };

        window.ApplyPlatform(PlatformProfiles.ForCurrentHost());
        _ = viewModel.InitializeAsync();
        window.Show();

        var table = FindOrders(viewModel);
        table.OpenQueryCommand.Execute(null);

        viewModel.Query.Sql = "SELECT region FROM dbo.orders";
        viewModel.Query.RunCommand.Execute(null);
        WaitFor(() => viewModel.Query.Tabs.Count > 0);

        // ビューモデルだけでなく、グリッドのテンプレートが実際に組み上がることまで見る。
        // 見出しとセルは別のテンプレートなので、両方が出ることを確かめる。
        WaitFor(() => Texts(window).Contains("北米"));
        Assert.Contains("region", Texts(window));
    }

    [AvaloniaFact]
    public void エクスプローラーは既定幅で並びスプリッターで動かせる()
    {
        var window = CreateWindow(out _);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 幅は列側が持つ（ペイン側に Width を置くとスプリッターで動かせない）。
        var pane = window.GetVisualDescendants().OfType<ObjectExplorerPane>().Single();
        Assert.Equal(288, (int)pane.Bounds.Width);

        // 作業領域にも（エディタと結果の間に）スプリッターがあるので、横方向のものだけを数える。
        Assert.Single(
            window.GetVisualDescendants().OfType<GridSplitter>(),
            splitter => splitter.ResizeDirection == GridResizeDirection.Columns);
    }

    [AvaloniaFact]
    public void 環境タグと読み取り専用がステータスに出る()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        Assert.Equal("本番", viewModel.EnvironmentName);
        Assert.True(viewModel.IsCritical);
        Assert.True(viewModel.IsReadOnly);
        Assert.Equal("SQL Server 2022 (16.0.4215.2)", viewModel.ServerDescription);
    }

    [AvaloniaFact]
    public async Task ウィンドウを閉じるとセッションを閉じる()
    {
        var session = NewSession();
        var viewModel = NewViewModel(session);

        await viewModel.DisposeAsync();

        Assert.True(session.IsDisposed);
    }

    private static MainWindow CreateWindow(out MainWindowViewModel viewModel)
    {
        viewModel = NewViewModel(NewSession());
        var window = new MainWindow { DataContext = viewModel };

        window.ApplyPlatform(PlatformProfiles.ForCurrentHost());
        _ = viewModel.InitializeAsync();

        return window;
    }

    private static MainWindowViewModel NewViewModel(FakeDatabaseSession session)
    {
        var query = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        var tableEditor = new TableEditorViewModel(
            session,
            new EditTableRowsUseCase(),
            new UpdateTableCellUseCase(),
            new InsertTableRowUseCase(),
            new DeleteTableRowUseCase(),
            new FakeRowDeletionPrompt());

        return new MainWindowViewModel(
            session,
            PlatformProfiles.ForCurrentHost(),
            new CatalogContext(
                session,
                new ListDatabasesUseCase(),
                new ListSchemasUseCase(),
                new ListTablesUseCase(),
                new ListColumnsUseCase(),
                new ListStoredProceduresUseCase(),
                new ListStoredProcedureParametersUseCase(),
                query)
            {
                TableEditor = tableEditor,
                Security = new DatabaseSecurityContext(new ListDatabaseUsersUseCase())
            },
            query,
            tableEditor);
    }

    private static FakeDatabaseSession NewSession()
    {
        var dbo = new SchemaName("dbo");

        // 見本データの先頭は本番タグの接続なので、環境タグと読み取り専用の表示も一緒に確かめられる。
        return new FakeDatabaseSession
        {
            Databases = [new DatabaseDescriptor(new DatabaseName("sales_db"))]
        }
        .WithSchemas("sales_db", new SchemaDescriptor(dbo))
        .WithTables("sales_db", "dbo", new TableDescriptor(dbo, "orders", 8_400_000))
        .WithDatabaseUsers("sales_db",
            new DatabaseUserDescriptor(
                new DatabaseUserName("app_user"), DatabaseUserType.SqlUserWithLogin, "app_login", dbo));
    }

    /// <summary>今そこに描かれている文字列。テンプレートが組み上がったかを見るのに使う。</summary>
    private static IReadOnlyList<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

    /// <summary>画面と同じ手順でツリーを開き、sales_db.dbo.orders のノードを取り出す。</summary>
    private static TableNode FindOrders(MainWindowViewModel viewModel)
    {
        WaitFor(() => viewModel.Explorer.Roots.FirstOrDefault()?.Children.Count > 0);

        var databases = viewModel.Explorer.Roots[0].Children[0];
        WaitFor(() => databases.Children.OfType<DatabaseNode>().Any());

        var schemas = Expand<SchemaNode>(
            databases.Children.OfType<DatabaseNode>().First(node => node.Title == "sales_db"),
            "スキーマ");

        return Expand<TableNode>(schemas.First(node => node.Title == "dbo"), "テーブル")
            .First(node => node.Title == "orders");
    }

    /// <summary>
    /// ノードと、その下の名指しした見出しを画面と同じ手順で開き、出てきた子を返す。
    ///
    /// 見出しは型では選べない。データベースの下には「スキーマ」（<see cref="SchemasNode"/>）と
    /// 「セキュリティ」（<see cref="CatalogFolderNode"/>）が並ぶように、種類の違う見出しが
    /// 同じ階層に混ざるため。
    /// </summary>
    private static IReadOnlyList<T> Expand<T>(ObjectExplorerNode node, string folderTitle)
        where T : ObjectExplorerNode
    {
        node.IsExpanded = true;
        WaitFor(() => node.Children.Any(child => child.Title == folderTitle));

        var folder = node.Children.First(child => child.Title == folderTitle);
        folder.IsExpanded = true;
        WaitFor(() => folder.Children.OfType<T>().Any());

        return folder.Children.OfType<T>().ToList();
    }

    private static void WaitFor(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "期待した状態になりませんでした。");
    }
}
