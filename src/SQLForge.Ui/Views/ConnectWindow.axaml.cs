using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SQLForge.Application.Abstractions;

namespace SQLForge.Ui.Views;

/// <summary>
/// 起動時に出る接続ダイアログ。ウィンドウ装飾だけが OS ごとに変わるので、
/// その分岐を <see cref="ApplyPlatform"/> に閉じ込めてある。
/// </summary>
public partial class ConnectWindow : Window
{
    private bool _usesCustomTitleBar = true;

    public ConnectWindow() => InitializeComponent();

    /// <summary>
    /// macOS は OS 側が信号機ボタンを描くので標準の装飾に任せ、
    /// Linux / Windows ではモックアップどおり自前のタイトルバーを使う。
    /// </summary>
    public void ApplyPlatform(IPlatformProfile platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        _usesCustomTitleBar = !platform.PrefersNativeTitleBar;

        ExtendClientAreaToDecorationsHint = _usesCustomTitleBar;
        ExtendClientAreaChromeHints = _usesCustomTitleBar
            ? Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome
            : Avalonia.Platform.ExtendClientAreaChromeHints.Default;

        if (this.FindControl<Button>("CloseButton") is { } closeButton)
        {
            closeButton.IsVisible = _usesCustomTitleBar;
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_usesCustomTitleBar && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
