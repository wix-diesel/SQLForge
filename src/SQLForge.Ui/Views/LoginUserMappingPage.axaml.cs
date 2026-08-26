using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>ログインのプロパティ ダイアログの「ユーザー マッピング」ページ。</summary>
public partial class LoginUserMappingPage : UserControl
{
    public LoginUserMappingPage() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
