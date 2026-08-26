using SQLForge.Application.Security;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ツリーのサーバー直下に「セキュリティ」の枝がいるときに要る一式。
/// これが無い構成（ツリーだけを組むとき）では枝そのものを出さない。
/// </summary>
/// <param name="Logins">ログイン一覧のユースケース。</param>
/// <param name="Editor">右クリックの追加・編集・削除の行き先。無ければメニューを出さない。</param>
public sealed record ServerSecurityContext(
    ListServerLoginsUseCase Logins,
    IServerLoginEditor? Editor = null)
{
    /// <summary>ロール一覧のユースケース。無ければ「サーバー ロール」の見出しを出さない。</summary>
    public ListServerRolesUseCase? Roles { get; init; }

    /// <summary>ロールの追加・編集・削除の行き先。無ければメニューを出さない。</summary>
    public IServerRoleEditor? RoleEditor { get; init; }
}
