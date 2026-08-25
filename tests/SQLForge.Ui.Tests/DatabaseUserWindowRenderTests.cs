using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ユーザーのダイアログが実際に組み上がって描けること。
/// XAML の記述ミスやリソース参照の取りこぼしは、ここで初めて表に出る。
/// </summary>
public class DatabaseUserWindowRenderTests
{
    [AvaloniaFact]
    public void 新しいユーザーのダイアログが描画できる()
    {
        var window = CreateWindow(out _, user: null);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Contains("新しいデータベース ユーザー", Texts(window));
    }

    [AvaloniaFact]
    public void 候補のスキーマとロールが欄に並ぶ()
    {
        var window = CreateWindow(out var dialog, user: null);
        window.Show();

        WaitFor(() => dialog.Roles.Count > 0);
        Dispatcher.UIThread.RunJobs();

        // メンバーシップはチェックボックスとして実際に行が組まれる。
        var roles = window.GetVisualDescendants().OfType<CheckBox>().Select(box => box.Content as string).ToList();

        Assert.Equal(["db_datareader", "db_datawriter"], roles);
        Assert.Equal(["dbo", "sales"], dialog.SchemaChoices);
    }

    [AvaloniaFact]
    public void ロールが多くても一番下の選択肢まで届く()
    {
        // 画面全体と一覧の両方をスクロールできる作りにすると、一覧の上でホイールを回しても
        // 外側が動かず、箱の下端が画面外に残って最後の行に手が届かなくなる。
        var window = CreateWindow(out var dialog, user: null, roles: ManyRoles);
        window.Show();

        WaitFor(() => dialog.Roles.Count == ManyRoles.Length);
        Render(window);

        // 一覧を囲むスクロールが他にいないこと。いると一覧が途中で切り取られ、
        // ホイールも内側に吸われるので、箱の下端に手が届かなくなる。
        var list = RoleList(window);
        Assert.Empty(list.GetVisualAncestors().OfType<ScrollViewer>());

        // 見本のロールは欄に収まりきらない数であること（そうでないと以下を確かめられない）。
        Assert.True(list.Extent.Height > list.Viewport.Height, "見本のロールが欄に収まってしまっています。");

        // 端まで送れば、最後のロールが欄の中にすべて入ること。
        // 送れる長さ（Extent）が中身より短いと、どこまで送っても最後の 1 行が欄の外に残る。
        list.ScrollToEnd();
        Render(window);

        var last = window.GetVisualDescendants().OfType<CheckBox>().Last();
        Assert.Equal(ManyRoles[^1], last.Content as string);
        Assert.True(
            Bottom(last, window) <= Bottom(list, window),
            $"最後のロールが欄からはみ出しています（行の下端 {Bottom(last, window)}、欄の下端 {Bottom(list, window)}）。");
    }

    [AvaloniaFact]
    public void ログインなしを選ぶとログイン欄が消える()
    {
        var window = CreateWindow(out var dialog, user: null);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(LoginBox(window).IsEffectivelyVisible);

        dialog.SelectedType = dialog.TypeChoices.First(
            choice => choice.Value == DatabaseUserType.SqlUserWithoutLogin);
        Dispatcher.UIThread.RunJobs();

        Assert.False(LoginBox(window).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void 編集では今の値が入って種類は選べない()
    {
        var user = new DatabaseUserDescriptor(
            new DatabaseUserName("app_user"),
            DatabaseUserType.SqlUserWithLogin,
            "app_login",
            new SchemaName("sales"));

        var window = CreateWindow(out _, user);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("データベース ユーザー — app_user", Texts(window));

        var types = window.GetVisualDescendants().OfType<ComboBox>().First();
        Assert.False(types.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void 確認ダイアログが描画できる()
    {
        var confirm = ConfirmDialogViewModel.Destructive(
            "オブジェクトの削除", "ユーザー app_user を削除しますか？", "この操作は取り消せません。", "削除");
        var window = new ConfirmWindow { DataContext = confirm };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Contains("ユーザー app_user を削除しますか？", Texts(window));

        // 見出しはビューモデルが決める（窓の題も含めて）。
        Assert.Equal("オブジェクトの削除", window.Title);

        // 取り消せない操作の確定ボタンは危険色にする。
        var button = ConfirmButton(window);
        Assert.Equal("削除", button.Content as string);
        Assert.Contains("danger", button.Classes);
    }

    [AvaloniaFact]
    public void 知らせるだけのダイアログにはキャンセルを出さない()
    {
        var alert = ConfirmDialogViewModel.Alert("SQLForge", "削除できませんでした", "権限がありません。");
        var window = new ConfirmWindow { DataContext = alert };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Button>(),
            button => button.IsVisible && button.Content as string == "キャンセル");

        // 知らせるだけなので危険色にはしない。
        Assert.DoesNotContain("danger", ConfirmButton(window).Classes);
    }

    /// <summary>確認ダイアログの確定ボタン（既定の動作を持つほう）。</summary>
    private static Button ConfirmButton(Window window) =>
        window.GetVisualDescendants().OfType<Button>().First(button => button.IsDefault);

    /// <summary>SQL Server が最初から持っている固定ロール。箱に収まりきらない数の見本として使う。</summary>
    private static readonly string[] ManyRoles =
    [
        "db_accessadmin", "db_backupoperator", "db_datareader", "db_datawriter", "db_ddladmin",
        "db_denydatareader", "db_denydatawriter", "db_owner", "db_securityadmin"
    ];

    private static DatabaseUserWindow CreateWindow(
        out DatabaseUserDialogViewModel dialog,
        DatabaseUserDescriptor? user,
        string[]? roles = null)
    {
        var session = new FakeDatabaseSession()
            .WithSchemas("sales_db",
                new SchemaDescriptor(new SchemaName("dbo")),
                new SchemaDescriptor(new SchemaName("sales")),
                new SchemaDescriptor(new SchemaName("sys"), IsSystem: true))
            .WithDatabaseRoles("sales_db", roles ?? ["db_datawriter", "db_datareader"]);

        dialog = new DatabaseUserDialogViewModel(
            session,
            new DatabaseName("sales_db"),
            user,
            new ListSchemasUseCase(),
            new ListDatabaseRolesUseCase(),
            new SaveDatabaseUserUseCase());

        var window = new DatabaseUserWindow { DataContext = dialog };
        _ = dialog.InitializeAsync();

        return window;
    }

    /// <summary>
    /// 実際に 1 枚描かせて配置を確定させる。<see cref="Dispatcher.RunJobs()"/> だけでは
    /// スクロール量が配置へ反映されず、位置を測っても当てにならない。
    /// </summary>
    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
    }

    /// <summary>囲みから見たコントロールの下端。中に収まっているかを見るのに使う。</summary>
    private static double Bottom(Visual control, Visual container) =>
        control.TranslatePoint(new Point(0, control.Bounds.Height), container)!.Value.Y;

    /// <summary>メンバーシップの一覧。ダイアログで唯一スクロールする欄。</summary>
    private static ScrollViewer RoleList(Window window) =>
        window.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .First(scroll => scroll.GetVisualDescendants().OfType<CheckBox>().Any());

    /// <summary>ログイン名の入力欄。隠れていても視覚ツリーには残るので、見え方で確かめる。</summary>
    private static TextBox LoginBox(Window window) =>
        window.GetVisualDescendants().OfType<TextBox>().First(box => box.PlaceholderText == "サーバー ログイン名");

    /// <summary>今そこに描かれている文字列。テンプレートが組み上がったかを見るのに使う。</summary>
    private static IReadOnlyList<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

    private static void WaitFor(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "期待した状態になりませんでした。");
    }
}
