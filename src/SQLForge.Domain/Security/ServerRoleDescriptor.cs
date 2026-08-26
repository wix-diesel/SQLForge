namespace SQLForge.Domain.Security;

/// <summary>
/// サーバー上のロール 1 件。SSMS の [サーバー] → [セキュリティ] → [サーバー ロール] に並ぶもの。
/// </summary>
/// <param name="Name">ロール名。</param>
/// <param name="Owner">ロールの所有者（ログインまたは別のサーバー ロール）。読めないときは null。</param>
/// <param name="IsFixedRole">
/// エンジンが用意した固定サーバー ロール（sysadmin・dbcreator など）。
/// メンバーの出し入れはできるが、ロールそのものは作り替えられない。
/// </param>
public sealed record ServerRoleDescriptor(
    RoleName Name,
    string? Owner = null,
    bool IsFixedRole = false)
{
    /// <summary>このロールに入っているログインとロール。</summary>
    public IReadOnlyList<string> Members { get; init; } = [];

    /// <summary>このロールが入っている別のサーバー ロール。SSMS の「メンバーシップ」にあたる。</summary>
    public IReadOnlyList<string> Memberships { get; init; } = [];

    /// <summary>ツリーで控えめに出すか。固定ロールはエンジンが用意したもの。</summary>
    public bool IsSystem => IsFixedRole;

    /// <summary>
    /// 名前・所有者を作り替えてよいか。固定ロールでもメンバーの出し入れはできるので、
    /// プロパティを開くこと自体は妨げない。
    /// </summary>
    public bool IsEditable => !IsFixedRole;
}
