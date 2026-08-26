using Avalonia.Controls;
using SQLForge.Ui.ViewModels;

namespace SQLForge.Ui.Views;

/// <summary>
/// セキュリティ関係のダイアログを出す口で共通の足回り。
/// 「親の上にモーダルで出す・確認を取る・失敗の理由を見せる」という形は
/// ユーザー・ログイン・ロール・スキーマのどれでも変わらないので、ここにまとめる。
///
/// ダイアログを出すのはビューの仕事なので、<see cref="Window"/> を知ってよいのはここだけ。
/// 親ウィンドウは接続が通ったあとにしか決まらないので、あとから <see cref="Owner"/> に差す。
/// </summary>
public abstract class SecurityDialogService
{
    /// <summary>モーダルの親。メインウィンドウが開いたときに差す。</summary>
    public Window? Owner { get; set; }

    /// <summary>取り消せない操作の確認。押されたら true。</summary>
    protected Task<bool> ConfirmAsync(string headline, string detail, string confirmLabel = "削除") =>
        ShowAsync(ConfirmDialogViewModel.Destructive("オブジェクトの削除", headline, detail, confirmLabel));

    /// <summary>失敗の理由を見せる。</summary>
    protected async Task ShowAlertAsync(string headline, string detail) =>
        await ShowAsync(ConfirmDialogViewModel.Alert("SQLForge", headline, detail)).ConfigureAwait(true);

    protected Task<bool> ShowAsync(ConfirmDialogViewModel dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var window = new ConfirmWindow { DataContext = dialog };
        dialog.CloseRequested += (_, result) => window.Close(result);

        return ShowAsync(window);
    }

    /// <summary>
    /// 親の上にモーダルで出す。閉じるのはビューモデルの合図（CloseRequested）にそろえるが、
    /// 利用者が窓の × で閉じたときは <see cref="Window.ShowDialog{TResult}"/> が
    /// 既定値（false）を返すので、「やめた」と同じ扱いになる。
    /// </summary>
    protected Task<bool> ShowAsync(Window window) =>
        Owner is { } owner
            ? window.ShowDialog<bool>(owner)
            : throw new InvalidOperationException("ダイアログの親ウィンドウが決まっていません。");

    /// <summary>
    /// 削除を実行し、失敗したら理由を見せる。確認は呼び出し側で取ってから渡すこと。
    /// </summary>
    protected async Task<bool> TryDeleteAsync(Func<Task> delete)
    {
        ArgumentNullException.ThrowIfNull(delete);

        try
        {
            await delete().ConfigureAwait(true);
            return true;
        }
        catch (Exception exception)
        {
            // 権限不足や、まだ何かがぶら下がっている場合はここへ来る。
            // 理由を出すだけで、ツリーはそのまま。
            await ShowAlertAsync("削除できませんでした", exception.Message).ConfigureAwait(true);
            return false;
        }
    }
}
