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

    /// <summary>起動時に出すのは接続ダイアログ。メインウィンドウはフェーズ 1 の次の段階。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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
        _ = viewModel.InitializeAsync();

        return window;
    }
}
