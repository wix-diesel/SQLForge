using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// 取り込みで手元の接続に当たったときの確認。
/// 窓の × で閉じられたときは <see cref="Window.ShowDialog{TResult}"/> が既定値を返すので、
/// <see cref="ViewModels.ImportConflictChoice.Cancel"/> を 0 にして「やめた」と同じ扱いにしてある。
/// </summary>
public partial class ImportConflictWindow : Window
{
    public ImportConflictWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
