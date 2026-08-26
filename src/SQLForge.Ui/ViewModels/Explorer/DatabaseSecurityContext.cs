using SQLForge.Application.Security;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ツリーの「セキュリティ」の枝がいるときに要る一式。
/// これが無い構成（ツリーだけを組むとき）では枝そのものを出さない。
/// </summary>
/// <param name="Users">ユーザー一覧のユースケース。</param>
/// <param name="Editor">右クリックの追加・編集・削除の行き先。無ければメニューを出さない。</param>
public sealed record DatabaseSecurityContext(
    ListDatabaseUsersUseCase Users,
    IDatabaseUserEditor? Editor = null)
{
    /// <summary>ロール一覧のユースケース。無ければ「データベース ロール」の見出しを出さない。</summary>
    public ListDatabaseRolesUseCase? Roles { get; init; }

    /// <summary>ロールの追加・編集・削除の行き先。無ければメニューを出さない。</summary>
    public IDatabaseRoleEditor? RoleEditor { get; init; }

    /// <summary>スキーマの追加・編集・削除の行き先。無ければメニューを出さない。</summary>
    public ISchemaEditor? SchemaEditor { get; init; }
}
