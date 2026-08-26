using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Ui.Composition;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.Views;

namespace SQLForge.Ui;

// SQLForge.Application 名前空間と Avalonia.Application 型の名前が衝突するため明示する。
public partial class App : Avalonia.Application
{
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// 起動時に出すのは接続ダイアログ。接続が通ったらメインウィンドウへ差し替える。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 接続ダイアログからメインウィンドウへ引き継ぐ間、どちらも「主ウィンドウ」でない
            // 瞬間があるので、最後の 1 枚が閉じたときに終わる方式にする。
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnLastWindowClose;

            _services = AppServices.Build();
            desktop.MainWindow = CreateConnectWindow(_services);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ConnectWindow CreateConnectWindow(IServiceProvider services)
    {
        var viewModel = services.GetRequiredService<ConnectDialogViewModel>();
        var window = new ConnectWindow { DataContext = viewModel };

        window.ApplyPlatform(services.GetRequiredService<IPlatformProfile>());
        viewModel.CloseRequested += (_, _) => window.Close();
        viewModel.ConnectionEstablished += (_, session) => ShowMainWindow(services, session, window);
        _ = viewModel.InitializeAsync();

        return window;
    }

    /// <summary>
    /// 開いたセッションをメインウィンドウへ渡し、それまでの窓（接続ダイアログ、または
    /// 接続解除で閉じる前のメインウィンドウ）を閉じる。セッションを閉じるのはメインウィンドウの
    /// ビューモデルの役目。
    /// </summary>
    private static void ShowMainWindow(IServiceProvider services, IDatabaseSession session, Avalonia.Controls.Window windowToClose)
    {
        var viewModel = services.GetRequiredService<MainWindowViewModelFactory>().Create(session);
        var window = new MainWindow { DataContext = viewModel };

        window.ApplyPlatform(services.GetRequiredService<IPlatformProfile>());
        viewModel.CloseRequested += (_, _) => window.Close();
        viewModel.DisconnectRequested += (_, _) => Disconnect(services, window);
        window.Closed += async (_, _) => await viewModel.DisposeAsync().ConfigureAwait(false);

        // ツリーから開くダイアログの親。ここで初めて決まる。
        services.GetRequiredService<DatabaseUserDialogService>().Owner = window;
        services.GetRequiredService<ServerLoginDialogService>().Owner = window;
        services.GetRequiredService<DatabaseRoleDialogService>().Owner = window;
        services.GetRequiredService<ServerRoleDialogService>().Owner = window;
        services.GetRequiredService<SchemaDialogService>().Owner = window;

        window.Show();
        _ = viewModel.InitializeAsync();

        windowToClose.Close();
    }

    /// <summary>
    /// 接続解除。新しい接続ダイアログを先に出してから今のメインウィンドウを閉じる
    /// （閉じる順を逆にすると、一瞬どちらの窓も無い状態になり OnLastWindowClose でアプリごと終わる）。
    /// </summary>
    private static void Disconnect(IServiceProvider services, MainWindow window)
    {
        CreateConnectWindow(services).Show();
        window.Close();
    }
}
