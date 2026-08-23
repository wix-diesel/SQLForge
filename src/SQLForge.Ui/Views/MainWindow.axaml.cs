using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SQLForge.Application.Abstractions;

namespace SQLForge.Ui.Views;

/// <summary>
/// 接続後のメインウィンドウ。ウィンドウ装飾の扱いは接続ダイアログと同じで、
/// OS ごとの分岐は <see cref="ApplyPlatform"/> に閉じ込めてある。
/// </summary>
public partial class MainWindow : Window
{
    private bool _usesCustomTitleBar = true;

    public MainWindow() => InitializeComponent();

    /// <summary>macOS は OS 側が信号機ボタンを描くので標準の装飾に任せる。</summary>
    public void ApplyPlatform(IPlatformProfile platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        _usesCustomTitleBar = !platform.PrefersNativeTitleBar;

        ExtendClientAreaToDecorationsHint = _usesCustomTitleBar;
        ExtendClientAreaChromeHints = _usesCustomTitleBar
            ? Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome
            : Avalonia.Platform.ExtendClientAreaChromeHints.Default;

        foreach (var name in new[] { "MinimizeButton", "MaximizeButton", "CloseButton" })
        {
            if (this.FindControl<Button>(name) is { } button)
            {
                button.IsVisible = _usesCustomTitleBar;
            }
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_usesCustomTitleBar && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnToggleMaximize(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
