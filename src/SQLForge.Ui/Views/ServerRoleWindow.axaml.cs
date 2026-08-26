using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// サーバー ロールのプロパティ ダイアログ。メインウィンドウの上にモーダルで出す。
/// 中身はページごとの <see cref="UserControl"/> に分かれている。
/// </summary>
public partial class ServerRoleWindow : Window
{
    public ServerRoleWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
