using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// サーバー ロールのプロパティ ダイアログの「全般」ページ。
/// 開いた直後は名前欄から入力できるようにする。
/// </summary>
public partial class ServerRoleGeneralPage : UserControl
{
    public ServerRoleGeneralPage()
    {
        InitializeComponent();

        Loaded += (_, _) => this.FindControl<TextBox>("NameBox")?.Focus();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
