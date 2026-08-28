using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの絞り込みを決めるダイアログ。SSMS の「フィルターの設定」にあたる。
/// 行の並びはビューモデルが決めるので、ここはひな型を読むだけ。
/// </summary>
public partial class ObjectFilterWindow : Window
{
    public ObjectFilterWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
