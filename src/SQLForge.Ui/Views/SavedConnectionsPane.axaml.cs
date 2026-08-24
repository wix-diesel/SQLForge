using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SQLForge.Ui.ViewModels;

namespace SQLForge.Ui.Views;

public partial class SavedConnectionsPane : UserControl
{
    public SavedConnectionsPane() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// 保存済み接続の行を押したら、その接続を開く。
    /// 選択が変わったこと自体を合図にすると、起動直後の選び直しやキーボードでの移動でも
    /// 繋ぎに行ってしまうので、押した操作だけを拾う。
    /// </summary>
    private void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ConnectDialogViewModel dialog &&
            (e.Source as StyledElement)?.DataContext is SavedConnectionItemViewModel item)
        {
            dialog.SavedConnections.Activate(item);
        }
    }
}
