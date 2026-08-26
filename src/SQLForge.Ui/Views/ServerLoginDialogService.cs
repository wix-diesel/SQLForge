using Avalonia.Controls;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの右クリックから呼ばれる <see cref="IServerLoginEditor"/> の実装。
/// ダイアログを出すのはビューの仕事なので、ここだけが <see cref="Window"/> を知る。
///
/// 親ウィンドウは接続が通ったあとにしか決まらないので、あとから <see cref="Owner"/> に差す。
/// </summary>
public sealed class ServerLoginDialogService(
    ListDatabasesUseCase databases,
    ListServerRolesUseCase roles,
    SaveServerLoginUseCase save,
    DropServerLoginUseCase drop) : IServerLoginEditor
{
    /// <summary>モーダルの親。メインウィンドウが開いたときに差す。</summary>
    public Window? Owner { get; set; }

    public Task<bool> CreateAsync(IDatabaseSession session) => ShowEditorAsync(session, original: null);

    public Task<bool> EditAsync(IDatabaseSession session, ServerLoginDescriptor login) =>
        ShowEditorAsync(session, login);

    public async Task<bool> DeleteAsync(IDatabaseSession session, ServerLoginDescriptor login)
    {
        ArgumentNullException.ThrowIfNull(login);

        var confirm = ConfirmDialogViewModel.Destructive(
            "オブジェクトの削除",
            $"ログイン {login.Name.Value} を削除しますか？",
            "このサーバーから削除します。ログインに対応づいたデータベース ユーザーは残り、"
                + "どのログインにも紐づかない状態になります。この操作は取り消せません。",
            "削除");

        if (!await ShowAsync(confirm).ConfigureAwait(true))
        {
            return false;
        }

        try
        {
            await drop.ExecuteAsync(session, login).ConfigureAwait(true);
            return true;
        }
        catch (Exception exception)
        {
            // 権限不足や、まだ繋いでいるセッションが残っている場合はここへ来る。
            // 理由を出すだけで、ツリーはそのまま。
            await ShowAlertAsync("削除できませんでした", exception.Message).ConfigureAwait(true);
            return false;
        }
    }

    private async Task<bool> ShowEditorAsync(IDatabaseSession session, ServerLoginDescriptor? original)
    {
        var dialog = new ServerLoginDialogViewModel(session, original, databases, roles, save);
        var window = new ServerLoginWindow { DataContext = dialog };

        // 候補（データベース・サーバー ロール）は開いてから読む。読めなくてもダイアログは出す。
        _ = dialog.InitializeAsync();

        dialog.CloseRequested += (_, result) => window.Close(result);

        return await ShowAsync(window).ConfigureAwait(true);
    }

    private async Task ShowAlertAsync(string headline, string detail) =>
        await ShowAsync(ConfirmDialogViewModel.Alert("SQLForge", headline, detail)).ConfigureAwait(true);

    private Task<bool> ShowAsync(ConfirmDialogViewModel dialog)
    {
        var window = new ConfirmWindow { DataContext = dialog };
        dialog.CloseRequested += (_, result) => window.Close(result);

        return ShowAsync(window);
    }

    /// <summary>
    /// 親の上にモーダルで出す。閉じるのはビューモデルの合図（CloseRequested）にそろえるが、
    /// 利用者が窓の × で閉じたときは <see cref="Window.ShowDialog{TResult}"/> が
    /// 既定値（false）を返すので、「やめた」と同じ扱いになる。
    /// </summary>
    private Task<bool> ShowAsync(Window window) =>
        Owner is { } owner
            ? window.ShowDialog<bool>(owner)
            : throw new InvalidOperationException("ダイアログの親ウィンドウが決まっていません。");
}
