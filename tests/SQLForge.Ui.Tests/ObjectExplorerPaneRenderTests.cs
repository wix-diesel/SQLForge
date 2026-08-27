using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Application.Catalog;
using SQLForge.Application.Editing;
using SQLForge.Application.Query;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
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
/// ツリーの行の組み方。ペインを狭くしても名前が消えないことを見る。
///
/// 行は「アイコン・名前・補足」の 3 つ組で、幅が足りないときに削るのは補足のほう。
/// 補足の側に必要なだけ幅を取らせると、名前が先に 0 幅まで潰れて補足だけが残ってしまう。
/// </summary>
public class ObjectExplorerPaneRenderTests
{
    /// <summary>モックアップの既定幅（288）より狭く、補足が入りきらない幅。</summary>
    private const int NarrowWidth = 220;

    [AvaloniaFact]
    public void ペインが狭くてもユーザー名は消えない()
    {
        var window = NarrowPane(out var viewModel);
        window.Show();

        var users = ExpandToUsers(viewModel);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("app_user", users.Children.OfType<DatabaseUserNode>().Single().Title);
        Assert.True(Width(window, "app_user") > 0, "ユーザー名が潰れて消えています。");
    }

    [AvaloniaFact]
    public void ペインが狭くてもログイン名は消えない()
    {
        var window = NarrowPane(out var viewModel);
        window.Show();

        var logins = ExpandToLogins(viewModel);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("app_login", logins.Children.OfType<ServerLoginNode>().Single().Title);
        Assert.True(Width(window, "app_login") > 0, "ログイン名が潰れて消えています。");
    }

    [AvaloniaFact]
    public void ペインが狭くてもカラム名は消えない()
    {
        // カラムの補足（PK, IDENTITY, int, not null）はユーザーの補足よりさらに長い。
        var window = NarrowPane(out var viewModel);
        window.Show();

        ExpandToColumns(viewModel);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Width(window, "id") > 0, "カラム名が潰れて消えています。");
    }

    [AvaloniaFact]
    public void 広ければ名前も補足も両方出る()
    {
        var window = NarrowPane(out var viewModel);
        window.Width = 420;
        window.Show();

        var users = ExpandToUsers(viewModel);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Width(window, "app_user") > 0);
        Assert.True(Width(window, "SQL ユーザー（ログインあり） · app_login") > 0);

        // 補足は右端に寄せる（名前のすぐ隣ではなく、行の端で揃える）。
        var name = Block(window, "app_user");
        var detail = Block(window, "SQL ユーザー（ログインあり） · app_login");
        var pane = window.GetVisualDescendants().OfType<ObjectExplorerPane>().Single();

        var nameRight = name.TranslatePoint(new Point(name.Bounds.Width, 0), pane)!.Value.X;
        var detailLeft = detail.TranslatePoint(default, pane)!.Value.X;

        Assert.True(detailLeft > nameRight, "補足が名前のすぐ隣に来ています。");
    }

    /// <summary>ペインだけを狭い窓に入れる。ツリーの行の組み方だけを見たいので周りは持たない。</summary>
    private static Window NarrowPane(out MainWindowViewModel viewModel)
    {
        viewModel = NewViewModel();

        var window = new Window
        {
            Width = NarrowWidth,
            Height = 400,
            Content = new ObjectExplorerPane { DataContext = viewModel }
        };

        _ = viewModel.InitializeAsync();

        return window;
    }

    private static ObjectExplorerNode ExpandToUsers(MainWindowViewModel viewModel)
    {
        var database = Database(viewModel);
        var security = Expand(database, "セキュリティ");
        var users = Expand(security, "ユーザー");

        WaitFor(() => users.Children.OfType<DatabaseUserNode>().Any());

        return users;
    }

    /// <summary>サーバー直下のセキュリティ（データベースの下のものとは別）を開く。</summary>
    private static ObjectExplorerNode ExpandToLogins(MainWindowViewModel viewModel)
    {
        WaitFor(() => viewModel.Explorer.Roots.FirstOrDefault()?.Children.Count > 1);

        var security = Expand(viewModel.Explorer.Roots[0], "セキュリティ");
        var logins = Expand(security, "ログイン");

        WaitFor(() => logins.Children.OfType<ServerLoginNode>().Any());

        return logins;
    }

    private static void ExpandToColumns(MainWindowViewModel viewModel)
    {
        var schemas = Expand(Database(viewModel), "スキーマ");
        var dbo = Expand(schemas, "dbo");
        var tables = Expand(dbo, "テーブル");
        var orders = Expand(tables, "orders");
        var columns = Expand(orders, "列");

        WaitFor(() => columns.Children.OfType<ColumnNode>().Any());
    }

    private static ObjectExplorerNode Database(MainWindowViewModel viewModel)
    {
        WaitFor(() => viewModel.Explorer.Roots.FirstOrDefault()?.Children.Count > 0);

        var databases = viewModel.Explorer.Roots[0].Children[0];
        WaitFor(() => databases.Children.OfType<DatabaseNode>().Any());

        return databases.Children.OfType<DatabaseNode>().First();
    }

    private static ObjectExplorerNode Expand(ObjectExplorerNode node, string childTitle)
    {
        node.IsExpanded = true;
        WaitFor(() => node.Children.Any(child => child.Title == childTitle));

        var child = node.Children.First(candidate => candidate.Title == childTitle);
        child.IsExpanded = true;

        return child;
    }

    /// <summary>その文字列を出している行の幅。0 なら潰れて見えていない。</summary>
    private static double Width(Window window, string text) => Block(window, text).Bounds.Width;

    private static TextBlock Block(Window window, string text) =>
        window.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);

    private static MainWindowViewModel NewViewModel()
    {
        var dbo = new SchemaName("dbo");
        var session = new FakeDatabaseSession
        {
            Databases = [new DatabaseDescriptor(new DatabaseName("sales_db"))]
        }
        .WithSchemas("sales_db", new SchemaDescriptor(dbo))
        .WithTables("sales_db", "dbo", new TableDescriptor(dbo, "orders", 8_400_000))
        .WithColumns("sales_db", "dbo", "orders",
            new ColumnDescriptor("id", 1, "int", IsNullable: false, IsIdentity: true, IsPrimaryKey: true))
        .WithDatabaseUsers("sales_db",
            new DatabaseUserDescriptor(
                new DatabaseUserName("app_user"), DatabaseUserType.SqlUserWithLogin, "app_login", dbo))
        .WithServerLogins(
            new ServerLoginDescriptor(
                new ServerLoginName("app_login"),
                ServerLoginType.SqlLogin,
                new DatabaseName("sales_db")));

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
                Security = new DatabaseSecurityContext(new ListDatabaseUsersUseCase()),
                ServerSecurity = new ServerSecurityContext(new ListServerLoginsUseCase())
            },
            query,
            tableEditor);
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
