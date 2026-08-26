using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Ui.ViewModels.Workspace;

namespace SQLForge.Ui.Views;

/// <summary>
/// 先頭 N 行の編集グリッド。
///
/// セルの開け閉めだけはここで受ける。「押したら開く」「Enter で確定」「Esc で戻す」は
/// 入力装置の作法であって、ビューモデルに持たせると画面の都合が中まで染み出すため
/// （どの値になるか・書き戻すかどうかの判断はビューモデル側にある）。
/// </summary>
public partial class TableEditorPane : UserControl
{
    public TableEditorPane() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>セルを押したら開く。書き換えられない列では何も起きない。</summary>
    private void OnCellTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: EditableCellViewModel cell } control)
        {
            return;
        }

        cell.BeginEdit();
        FocusEditor(control);
    }

    /// <summary>
    /// 編集中のキー。SSMS と同じ割り当てにする
    /// （Enter・Tab で確定、Esc で元へ戻す、Ctrl+0 で NULL）。
    /// </summary>
    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: EditableCellViewModel cell })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter or Key.Tab:
                e.Handled = true;
                _ = cell.CommitAsync();
                break;

            case Key.Escape:
                e.Handled = true;
                cell.CancelEdit();
                break;

            case Key.D0 or Key.NumPad0 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                e.Handled = true;
                _ = cell.SetNullAsync();
                break;
        }
    }

    /// <summary>ほかを押して離れたときも確定する（SSMS と同じ）。</summary>
    private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: EditableCellViewModel cell })
        {
            _ = cell.CommitAsync();
        }
    }

    /// <summary>
    /// 開いたセルの入力欄へ焦点を移す。入力欄は開いた時点ではまだ出ていないので、
    /// レイアウトが一巡してから探す。
    /// </summary>
    private static void FocusEditor(Control cell) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (cell.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is not { } editor)
                {
                    return;
                }

                editor.Focus();
                editor.SelectAll();
            },
            DispatcherPriority.Input);
}
