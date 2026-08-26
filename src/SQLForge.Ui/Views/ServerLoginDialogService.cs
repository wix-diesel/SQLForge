using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの右クリックから呼ばれる <see cref="IServerLoginEditor"/> の実装。
/// モーダルの出し方と確認の取り方は <see cref="SecurityDialogService"/> から借りる。
/// </summary>
public sealed class ServerLoginDialogService(
    ListDatabasesUseCase databases,
    ListServerRolesUseCase roles,
    ListDatabaseRolesUseCase databaseRoles,
    ListLoginUserMappingsUseCase mappings,
    SaveServerLoginUseCase save,
    DropServerLoginUseCase drop,
    ListPermissionsUseCase permissions,
    ListSecurablesUseCase securables,
    SavePermissionsUseCase savePermissions) : SecurityDialogService, IServerLoginEditor
{
    public Task<bool> CreateAsync(IDatabaseSession session) => ShowEditorAsync(session, original: null);

    public Task<bool> EditAsync(IDatabaseSession session, ServerLoginDescriptor login) =>
        ShowEditorAsync(session, login);

    public async Task<bool> DeleteAsync(IDatabaseSession session, ServerLoginDescriptor login)
    {
        ArgumentNullException.ThrowIfNull(login);

        var confirmed = await ConfirmAsync(
                $"ログイン {login.Name.Value} を削除しますか？",
                "このサーバーから削除します。ログインに対応づいたデータベース ユーザーは残り、"
                    + "どのログインにも紐づかない状態になります。この操作は取り消せません。")
            .ConfigureAwait(true);

        // 権限不足や、まだ繋いでいるセッションが残っていれば失敗する。
        // そのときは理由を出すだけで、ツリーはそのまま。
        return confirmed && await TryDeleteAsync(() => drop.ExecuteAsync(session, login)).ConfigureAwait(true);
    }

    private async Task<bool> ShowEditorAsync(IDatabaseSession session, ServerLoginDescriptor? original)
    {
        // 新しいログインにはまだ権限も対応づけも無いので、名前は渡さない（読みにいかない）。
        var page = new SecurablePermissionsViewModel(
            session, SecurityPrincipalKind.ServerLogin, original?.Name.Value, database: null, permissions, securables);

        var mapping = new LoginUserMappingsViewModel(
            session,
            original?.Name.Value ?? string.Empty,
            isNewLogin: original is null,
            databases,
            databaseRoles,
            mappings);

        var dialog = new ServerLoginDialogViewModel(
            session, original, databases, roles, save, savePermissions, mapping, page);
        var window = new ServerLoginWindow { DataContext = dialog };

        // 候補（データベース・サーバー ロール）は開いてから読む。読めなくてもダイアログは出す。
        _ = dialog.InitializeAsync();

        dialog.CloseRequested += (_, result) => window.Close(result);

        return await ShowAsync(window).ConfigureAwait(true);
    }
}
