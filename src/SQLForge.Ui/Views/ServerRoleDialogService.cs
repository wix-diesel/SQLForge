using SQLForge.Application.Abstractions;
using SQLForge.Application.Security;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの右クリックから呼ばれる <see cref="IServerRoleEditor"/> の実装。
/// モーダルの出し方と確認の取り方は <see cref="SecurityDialogService"/> から借りる。
/// </summary>
public sealed class ServerRoleDialogService(
    ListServerLoginsUseCase logins,
    ListServerRolesUseCase roles,
    SaveServerRoleUseCase save,
    DropServerRoleUseCase drop,
    ListPermissionsUseCase permissions,
    ListSecurablesUseCase securables,
    SavePermissionsUseCase savePermissions) : SecurityDialogService, IServerRoleEditor
{
    public Task<bool> CreateAsync(IDatabaseSession session) => ShowEditorAsync(session, original: null);

    public Task<bool> EditAsync(IDatabaseSession session, ServerRoleDescriptor role) =>
        ShowEditorAsync(session, role);

    public async Task<bool> DeleteAsync(IDatabaseSession session, ServerRoleDescriptor role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var confirmed = await ConfirmAsync(
                $"サーバー ロール {role.Name.Value} を削除しますか？",
                "このサーバーから削除します。メンバーが残っているロールは削除できません。"
                    + "この操作は取り消せません。")
            .ConfigureAwait(true);

        return confirmed && await TryDeleteAsync(() => drop.ExecuteAsync(session, role)).ConfigureAwait(true);
    }

    private async Task<bool> ShowEditorAsync(IDatabaseSession session, ServerRoleDescriptor? original)
    {
        // 新しいロールにはまだ権限が無いので、名前は渡さない（読みにいかない）。
        var page = new SecurablePermissionsViewModel(
            session, SecurityPrincipalKind.ServerRole, original?.Name.Value, database: null, permissions, securables);

        var dialog = new ServerRoleDialogViewModel(
            session, original, logins, roles, save, savePermissions, page);

        var window = new ServerRoleWindow { DataContext = dialog };

        // 候補（ログイン・ロール）は開いてから読む。読めなくてもダイアログは出す。
        _ = dialog.InitializeAsync();

        dialog.CloseRequested += (_, result) => window.Close(result);

        return await ShowAsync(window).ConfigureAwait(true);
    }
}
