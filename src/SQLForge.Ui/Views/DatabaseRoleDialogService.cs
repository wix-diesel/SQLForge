using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの右クリックから呼ばれる <see cref="IDatabaseRoleEditor"/> の実装。
/// モーダルの出し方と確認の取り方は <see cref="SecurityDialogService"/> から借りる。
/// </summary>
public sealed class DatabaseRoleDialogService(
    ListDatabaseUsersUseCase users,
    ListDatabaseRolesUseCase roles,
    ListSchemasUseCase schemas,
    SaveDatabaseRoleUseCase save,
    DropDatabaseRoleUseCase drop,
    ListPermissionsUseCase permissions,
    ListSecurablesUseCase securables,
    SavePermissionsUseCase savePermissions) : SecurityDialogService, IDatabaseRoleEditor
{
    public Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database) =>
        ShowEditorAsync(session, database, original: null);

    public Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, DatabaseRoleDescriptor role) =>
        ShowEditorAsync(session, database, role);

    public async Task<bool> DeleteAsync(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseRoleDescriptor role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var confirmed = await ConfirmAsync(
                $"データベース ロール {role.Name.Value} を削除しますか？",
                $"{database.Value} から削除します。メンバーが残っているロールや、"
                    + "スキーマを所有しているロールは削除できません。この操作は取り消せません。")
            .ConfigureAwait(true);

        return confirmed
            && await TryDeleteAsync(() => drop.ExecuteAsync(session, database, role)).ConfigureAwait(true);
    }

    private async Task<bool> ShowEditorAsync(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseRoleDescriptor? original)
    {
        // 新しいロールにはまだ権限が無いので、名前は渡さない（読みにいかない）。
        var page = new SecurablePermissionsViewModel(
            session, SecurityPrincipalKind.DatabaseRole, original?.Name.Value, database, permissions, securables);

        var dialog = new DatabaseRoleDialogViewModel(
            session, database, original, users, roles, schemas, save, savePermissions, page);

        var window = new DatabaseRoleWindow { DataContext = dialog };

        // 候補（ユーザー・ロール・スキーマ）は開いてから読む。読めなくてもダイアログは出す。
        _ = dialog.InitializeAsync();

        dialog.CloseRequested += (_, result) => window.Close(result);

        return await ShowAsync(window).ConfigureAwait(true);
    }
}
