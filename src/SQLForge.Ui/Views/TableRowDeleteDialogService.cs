using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Workspace;

namespace SQLForge.Ui.Views;

/// <summary>
/// 編集グリッドの「行の削除」で出す確認。
/// モーダルの出し方は <see cref="SecurityDialogService"/> から借りる（親ウィンドウの差し方も同じ）。
/// </summary>
public sealed class TableRowDeleteDialogService : SecurityDialogService, IRowDeletionPrompt
{
    public Task<bool> ConfirmDeleteAsync(int rowCount) =>
        ShowAsync(ConfirmDialogViewModel.Destructive(
            "行の削除",
            $"{rowCount} 行を完全に削除します。",
            "削除した行は元に戻せません。続行しますか？",
            "削除"));
}
