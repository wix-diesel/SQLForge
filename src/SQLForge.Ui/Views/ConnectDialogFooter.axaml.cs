using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

public partial class ConnectDialogFooter : UserControl
{
    public ConnectDialogFooter() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
