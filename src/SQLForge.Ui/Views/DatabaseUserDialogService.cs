using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの右クリックから呼ばれる <see cref="IDatabaseUserEditor"/> の実装。
/// モーダルの出し方と確認の取り方は <see cref="SecurityDialogService"/> から借りる。
/// </summary>
public sealed class DatabaseUserDialogService(
    ListSchemasUseCase schemas,
    ListDatabaseRolesUseCase roles,
    SaveDatabaseUserUseCase save,
    DropDatabaseUserUseCase drop,
    ListPermissionsUseCase permissions,
    ListSecurablesUseCase securables,
    SavePermissionsUseCase savePermissions) : SecurityDialogService, IDatabaseUserEditor
{
    public Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database) =>
        ShowEditorAsync(session, database, original: null);

    public Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, DatabaseUserDescriptor user) =>
        ShowEditorAsync(session, database, user);

    public async Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, DatabaseUserDescriptor user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var confirmed = await ConfirmAsync(
                $"ユーザー {user.Name.Value} を削除しますか？",
                $"{database.Value} から削除します。ユーザーが所有しているスキーマがあると削除できません。"
                    + "この操作は取り消せません。")
            .ConfigureAwait(true);

        // 権限不足やスキーマの所有で失敗したら、理由を出すだけでツリーはそのまま。
        return confirmed
            && await TryDeleteAsync(() => drop.ExecuteAsync(session, database, user)).ConfigureAwait(true);
    }

    private async Task<bool> ShowEditorAsync(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseUserDescriptor? original)
    {
        // 新しいユーザーにはまだ権限が無いので、名前は渡さない（読みにいかない）。
        var page = new SecurablePermissionsViewModel(
            session, SecurityPrincipalKind.DatabaseUser, original?.Name.Value, database, permissions, securables);

        var dialog = new DatabaseUserDialogViewModel(
            session, database, original, schemas, roles, save, savePermissions, page);
        var window = new DatabaseUserWindow { DataContext = dialog };

        // 候補（スキーマ・ロール）は開いてから読む。読めなくてもダイアログは出す。
        _ = dialog.InitializeAsync();

        dialog.CloseRequested += (_, result) => window.Close(result);

        return await ShowAsync(window).ConfigureAwait(true);
    }
}
