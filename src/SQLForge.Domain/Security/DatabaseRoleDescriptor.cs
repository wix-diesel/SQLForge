namespace SQLForge.Domain.Security;

/// <summary>
/// データベース内のロール 1 件。SSMS の [データベース] → [セキュリティ] → [ロール] →
/// [データベース ロール] に並ぶもの。
/// </summary>
/// <param name="Name">ロール名。</param>
/// <param name="Owner">ロールの所有者（ユーザーまたは別のロール）。読めないときは null。</param>
/// <param name="IsFixedRole">
/// エンジンが用意した固定データベース ロール（db_owner・db_datareader など）。
/// メンバーの出し入れはできるが、ロールそのものは作り替えられない。
/// </param>
public sealed record DatabaseRoleDescriptor(
    RoleName Name,
    string? Owner = null,
    bool IsFixedRole = false)
{
    /// <summary>このロールに入っているユーザーとロール。</summary>
    public IReadOnlyList<string> Members { get; init; } = [];

    /// <summary>このロールが所有しているスキーマ。SSMS の「所有されているスキーマ」にあたる。</summary>
    public IReadOnlyList<string> OwnedSchemas { get; init; } = [];

    /// <summary>ツリーで控えめに出すか。固定ロールはエンジンが用意したもの。</summary>
    public bool IsSystem => IsFixedRole;

    /// <summary>
    /// 名前・所有者・所有スキーマを作り替えてよいか。固定ロールでも
    /// メンバーの出し入れはできるので、プロパティを開くこと自体は妨げない。
    /// </summary>
    public bool IsEditable => !IsFixedRole;
}
