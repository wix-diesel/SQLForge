using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

public partial class ObjectExplorerPane : UserControl
{
    public ObjectExplorerPane() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
