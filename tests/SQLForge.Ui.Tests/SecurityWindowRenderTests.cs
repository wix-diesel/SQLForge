using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 今回足したダイアログ（ロール・スキーマ）と、ページを増やしたダイアログが
/// 実際に組み上がって描けること。XAML の記述ミスやリソース参照の取りこぼしは、
/// ここで初めて表に出る。
/// </summary>
public class SecurityWindowRenderTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [AvaloniaFact]
    public void 新しいデータベースロールのダイアログが描画できる()
    {
        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb);
        var window = Show(new DatabaseRoleWindow { DataContext = dialog }, dialog.InitializeAsync());

        Assert.Contains("新しいデータベース ロール", Texts(window));
        Assert.Contains("このロールのメンバー", Texts(window));
    }

    [AvaloniaFact]
    public void 新しいサーバーロールのダイアログが描画できる()
    {
        var dialog = SecurityDialogs.ServerRole(NewSession());
        var window = Show(new ServerRoleWindow { DataContext = dialog }, dialog.InitializeAsync());

        Assert.Contains("新しいサーバー ロール", Texts(window));
        Assert.Contains("メンバーシップ（このロールが入るサーバー ロール）", Texts(window));
    }

    [AvaloniaFact]
    public void 新しいスキーマのダイアログが描画できる()
    {
        var dialog = SecurityDialogs.Schema(NewSession(), SalesDb);
        var window = Show(new SchemaWindow { DataContext = dialog }, dialog.InitializeAsync());

        Assert.Contains("新しいスキーマ", Texts(window));
        Assert.Contains("スキーマの所有者", Texts(window));
    }

    [AvaloniaFact]
    public void ロールのダイアログは権限のページへ切り替えられる()
    {
        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb);
        var window = Show(new DatabaseRoleWindow { DataContext = dialog }, dialog.InitializeAsync());

        dialog.SelectedPage = dialog.Pages[1];
        Dispatcher.UIThread.RunJobs();

        // ページを切り替えると、前のページの中身はツリーから消えて権限のページが組まれる。
        Assert.DoesNotContain("このロールのメンバー", Texts(window));
        Assert.Contains("一覧から外す", ButtonTexts(window));
    }

    [AvaloniaFact]
    public void ユーザーのダイアログは権限のページへ切り替えられる()
    {
        var dialog = SecurityDialogs.User(NewSession(), SalesDb);
        var window = Show(new DatabaseUserWindow { DataContext = dialog }, dialog.InitializeAsync());

        Assert.Equal(["全般", "セキュリティ保護可能なリソース"], dialog.Pages.Select(page => page.Title));

        dialog.SelectedPage = dialog.Pages[1];
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("一覧から外す", ButtonTexts(window));
    }

    [AvaloniaFact]
    public void ログインのダイアログはユーザーマッピングのページへ切り替えられる()
    {
        var session = NewSession();
        session.Databases = [new DatabaseDescriptor(SalesDb)];

        var dialog = SecurityDialogs.Login(session);
        var window = Show(new ServerLoginWindow { DataContext = dialog }, dialog.InitializeAsync());

        Assert.Equal(
            ["全般", "ユーザー マッピング", "セキュリティ保護可能なリソース"],
            dialog.Pages.Select(page => page.Title));

        dialog.SelectedPage = dialog.Pages[1];
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("このログインにマップされているユーザー", Texts(window));
    }

    /// <summary>開いて、候補の読み込みが済むまで走らせてから 1 枚描く。</summary>
    private static Window Show(Window window, Task initialize)
    {
        window.Show();

        for (var attempt = 0; attempt < 50 && !initialize.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        return window;
    }

    /// <summary>今そこに描かれている文字列。テンプレートが組み上がったかを見るのに使う。</summary>
    private static IReadOnlyList<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

    private static IReadOnlyList<string?> ButtonTexts(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string).ToList();

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession()
            .WithSchemas("sales_db",
                new SchemaDescriptor(new SchemaName("dbo")),
                new SchemaDescriptor(new SchemaName("sales")))
            .WithDatabaseUsers("sales_db",
                new DatabaseUserDescriptor(
                    new DatabaseUserName("app_user"),
                    DatabaseUserType.SqlUserWithLogin,
                    "app_login"))
            .WithDatabaseRoles("sales_db", "db_owner", "app_reader")
            .WithServerRoles("sysadmin", "dbcreator");
}
