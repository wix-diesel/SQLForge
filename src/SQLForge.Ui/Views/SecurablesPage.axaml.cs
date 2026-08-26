using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// 「セキュリティ保護可能なリソース」ページ。ユーザー・ログイン・ロールの
/// どのプロパティ ダイアログからも同じものを差し込む。
/// </summary>
public partial class SecurablesPage : UserControl
{
    public SecurablesPage() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
