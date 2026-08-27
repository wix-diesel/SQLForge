using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Ui.ViewModels;
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
            // Enter は行から出る操作なので、新しい行ではそこで 1 行として足す（SSMS と同じ）。
            case Key.Enter:
                e.Handled = true;
                _ = CommitRowAsync(cell);
                break;

            // Tab は隣のセルへ移るだけ。新しい行は打ちかけのまま残す。
            case Key.Tab:
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
    /// グリッドのキー。セルを開いていないときの Esc で、打ちかけの新しい行を取り消す
    /// （セルを開いているときの Esc はそのセルの打ちかけを捨てる。SSMS と同じ二段構え）。
    /// </summary>
    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape
            || DataContext is not MainWindowViewModel viewModel
            || viewModel.TableEditor.NewRow is not { HasPendingValues: true })
        {
            return;
        }

        e.Handled = true;
        viewModel.TableEditor.CancelNewRowCommand.Execute(null);
    }

    /// <summary>
    /// セルを確定してから、新しい行ならその行を足す。既存の行では行の確定は何もしない。
    /// </summary>
    private static async Task CommitRowAsync(EditableCellViewModel cell)
    {
        await cell.CommitAsync().ConfigureAwait(true);
        await cell.Row.CommitAsync().ConfigureAwait(true);
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
