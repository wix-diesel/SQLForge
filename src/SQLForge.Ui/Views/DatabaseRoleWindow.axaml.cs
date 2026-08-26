using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// データベース ロールのプロパティ ダイアログ。メインウィンドウの上にモーダルで出す。
/// 中身はページごとの <see cref="UserControl"/> に分かれている。
/// </summary>
public partial class DatabaseRoleWindow : Window
{
    public DatabaseRoleWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
