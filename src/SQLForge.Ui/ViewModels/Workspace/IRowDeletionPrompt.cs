namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 編集グリッドから行を消す前の確認を取る口。
///
/// SSMS の編集グリッドと同じで、行の削除は取り消せないので必ず一度尋ねる。
/// ダイアログを出すのはビューの受け持ちなので、ビューモデルはこの口だけを知る。
/// </summary>
public interface IRowDeletionPrompt
{
    /// <summary>消してよいか尋ねる。押されたら true。</summary>
    /// <param name="rowCount">消す行数。</param>
    Task<bool> ConfirmDeleteAsync(int rowCount);
}
