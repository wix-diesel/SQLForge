namespace SQLForge.Domain.Security;

/// <summary>
/// これから作る（あるいは作り替える）サーバー ロールのあるべき姿。
/// 検証を通った入力だけがこの形になり、ドライバーはこれを文面に写す。
/// </summary>
/// <param name="Name">ロール名。</param>
/// <param name="Owner">所有者。指定しないなら null（サーバーが実行した利用者を当てる）。</param>
public sealed record ServerRoleDefinition(RoleName Name, string? Owner = null)
{
    /// <summary>このロールに入れるログインとロール。</summary>
    public IReadOnlyList<string> Members { get; init; } = [];

    /// <summary>このロールを入れる別のサーバー ロール。固定ロールへの所属も含む。</summary>
    public IReadOnlyList<string> Memberships { get; init; } = [];
}
