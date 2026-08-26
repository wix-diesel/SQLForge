namespace SQLForge.Domain.Security;

/// <summary>
/// これから作る（あるいは作り替える）データベース ロールのあるべき姿。
/// 検証を通った入力だけがこの形になり、ドライバーはこれを文面に写す。
/// </summary>
/// <param name="Name">ロール名。</param>
/// <param name="Owner">所有者。指定しないなら null（サーバーが実行した利用者を当てる）。</param>
public sealed record DatabaseRoleDefinition(RoleName Name, string? Owner = null)
{
    /// <summary>このロールに入れるユーザーとロール。</summary>
    public IReadOnlyList<string> Members { get; init; } = [];

    /// <summary>このロールに持たせるスキーマ。外れたスキーマの持ち主は変更後の所有者ではなく元のまま。</summary>
    public IReadOnlyList<string> OwnedSchemas { get; init; } = [];
}
