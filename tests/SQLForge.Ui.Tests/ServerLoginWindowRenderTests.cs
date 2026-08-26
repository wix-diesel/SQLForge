using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ログインのダイアログが実際に組み上がって描けること。
/// XAML の記述ミスやリソース参照の取りこぼしは、ここで初めて表に出る。
/// </summary>
public class ServerLoginWindowRenderTests
{
    [AvaloniaFact]
    public void 新しいログインのダイアログが描画できる()
    {
        var window = CreateWindow(out _, login: null);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Contains("新しいログイン", Texts(window));
    }

    [AvaloniaFact]
    public void 候補のデータベースとサーバーロールが欄に並ぶ()
    {
        var window = CreateWindow(out var dialog, login: null);
        window.Show();

        WaitFor(() => dialog.Roles.Count > 0);
        Dispatcher.UIThread.RunJobs();

        // メンバーシップはチェックボックスとして実際に行が組まれる。
        // 上の「このログインを有効にする」も同じ CheckBox なので、末尾から数える。
        var roles = window.GetVisualDescendants()
            .OfType<CheckBox>()
            .Select(box => box.Content as string)
            .ToList();

        Assert.Equal(["dbcreator", "sysadmin"], roles.TakeLast(2));
        Assert.Equal(["sales_db", "master"], dialog.DatabaseChoices);
    }

    [AvaloniaFact]
    public void パスワードは伏せ字で入力する()
    {
        var window = CreateWindow(out _, login: null);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.All(PasswordBoxes(window), box => Assert.Equal('●', box.PasswordChar));
    }

    [AvaloniaFact]
    public void Windows認証を選ぶとパスワードの欄が消える()
    {
        var window = CreateWindow(out var dialog, login: null);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.All(PasswordBoxes(window), box => Assert.True(box.IsEffectivelyVisible));

        dialog.SelectedType = dialog.TypeChoices.First(choice => choice.Value == ServerLoginType.WindowsUser);
        Dispatcher.UIThread.RunJobs();

        Assert.All(PasswordBoxes(window), box => Assert.False(box.IsEffectivelyVisible));
    }

    [AvaloniaFact]
    public void 編集では今の値が入って認証方式は選べない()
    {
        var login = new ServerLoginDescriptor(
            new ServerLoginName("app_login"),
            ServerLoginType.SqlLogin,
            new DatabaseName("sales_db"))
        {
            PasswordPolicy = ServerLoginPasswordPolicy.Default
        };

        var window = CreateWindow(out _, login);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("ログイン — app_login", Texts(window));
        Assert.Contains("空のままにすると、今のパスワードをそのまま残します。", Texts(window));

        var types = window.GetVisualDescendants().OfType<ComboBox>().First();
        Assert.False(types.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void Windows認証のログインでは名前の欄を触らせない()
    {
        var login = new ServerLoginDescriptor(new ServerLoginName(@"CONTOSO\app"), ServerLoginType.WindowsUser);

        var window = CreateWindow(out _, login);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.GetVisualDescendants().OfType<TextBox>().First().IsEffectivelyEnabled);
    }

    /// <summary>パスワードと確認の 2 つ。伏せ字にしてあるかどうかで見分ける。</summary>
    private static IReadOnlyList<TextBox> PasswordBoxes(Window window) =>
        window.GetVisualDescendants().OfType<TextBox>().Where(box => box.PasswordChar != '\0').ToList();

    private static ServerLoginWindow CreateWindow(
        out ServerLoginDialogViewModel dialog,
        ServerLoginDescriptor? login)
    {
        var session = new FakeDatabaseSession
        {
            Databases =
            [
                new DatabaseDescriptor(new DatabaseName("sales_db")),
                new DatabaseDescriptor(new DatabaseName("master"), IsSystem: true)
            ]
        }
        .WithServerRoles("sysadmin", "dbcreator");

        dialog = new ServerLoginDialogViewModel(
            session,
            login,
            new ListDatabasesUseCase(),
            new ListServerRolesUseCase(),
            new SaveServerLoginUseCase());

        var window = new ServerLoginWindow { DataContext = dialog };
        _ = dialog.InitializeAsync();

        return window;
    }

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
