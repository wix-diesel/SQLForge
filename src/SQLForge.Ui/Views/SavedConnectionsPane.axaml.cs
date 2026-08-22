using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

public partial class SavedConnectionsPane : UserControl
{
    public SavedConnectionsPane() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
