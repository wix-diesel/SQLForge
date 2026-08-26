using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// ユーザーのプロパティ ダイアログ。メインウィンドウの上にモーダルで出すので、
/// タイトルバーは OS の装飾に任せる（接続ダイアログのような自前の装飾は持たない）。
///
/// 中身はページごとの <see cref="UserControl"/> に分かれていて、
/// 開いた直後のフォーカスもそちらの受け持ち。
/// </summary>
public partial class DatabaseUserWindow : Window
{
    public DatabaseUserWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
