using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// ログインのプロパティ ダイアログ。ユーザーのダイアログと同じく、
/// メインウィンドウの上にモーダルで出す。
///
/// 中身はページごとの <see cref="UserControl"/> に分かれていて、
/// 開いた直後のフォーカスもそちらの受け持ち。
/// </summary>
public partial class ServerLoginWindow : Window
{
    public ServerLoginWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
