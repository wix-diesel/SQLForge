using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// ユーザーのプロパティ ダイアログ。メインウィンドウの上にモーダルで出すので、
/// タイトルバーは OS の装飾に任せる（接続ダイアログのような自前の装飾は持たない）。
/// </summary>
public partial class DatabaseUserWindow : Window
{
    public DatabaseUserWindow()
    {
        InitializeComponent();

        // 開いた直後は名前欄から入力できるようにする。
        Opened += (_, _) => this.FindControl<TextBox>("NameBox")?.Focus();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
