using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Application.Catalog;
using SQLForge.Domain.Catalog;
using SQLForge.Infrastructure.Platform;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Explorer;
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
        var schemas = Expand<SchemaNode>(databases.Children.OfType<DatabaseNode>().First(node => node.Title == "sales_db"));
        Expand<TableNode>(schemas.First(node => node.Title == "dbo"));

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
    public void エクスプローラーは既定幅で並びスプリッターで動かせる()
    {
        var window = CreateWindow(out _);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 幅は列側が持つ（ペイン側に Width を置くとスプリッターで動かせない）。
        var pane = window.GetVisualDescendants().OfType<ObjectExplorerPane>().Single();
        Assert.Equal(288, (int)pane.Bounds.Width);
        Assert.Single(window.GetVisualDescendants().OfType<GridSplitter>());
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

        window.ApplyPlatform(new PlatformProfile());
        _ = viewModel.InitializeAsync();

        return window;
    }

    private static MainWindowViewModel NewViewModel(FakeDatabaseSession session) =>
        new(session,
            new PlatformProfile(),
            new CatalogContext(session, new ListDatabasesUseCase(), new ListSchemasUseCase(), new ListTablesUseCase()));

    private static FakeDatabaseSession NewSession()
    {
        var dbo = new SchemaName("dbo");

        // 見本データの先頭は本番タグの接続なので、環境タグと読み取り専用の表示も一緒に確かめられる。
        return new FakeDatabaseSession
        {
            Databases = [new DatabaseDescriptor(new DatabaseName("sales_db"))]
        }
        .WithSchemas("sales_db", new SchemaDescriptor(dbo))
        .WithTables("sales_db", "dbo", new TableDescriptor(dbo, "orders", 8_400_000));
    }

    /// <summary>ノードとその下の見出しノードを画面と同じ手順で開き、出てきた子を返す。</summary>
    private static IReadOnlyList<T> Expand<T>(ObjectExplorerNode node) where T : ObjectExplorerNode
    {
        node.IsExpanded = true;
        WaitFor(() => node.Children.OfType<CatalogFolderNode>().Any());

        var folder = node.Children.OfType<CatalogFolderNode>().First();
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
