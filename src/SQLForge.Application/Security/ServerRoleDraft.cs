using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// サーバー ロールのプロパティ ダイアログで編集中の入力値。
/// エンティティ（<see cref="ServerRoleDefinition"/>）は常に妥当である前提なので、
/// まだ妥当とは限らない入力はこの器で受け渡す。
/// </summary>
public sealed record ServerRoleDraft
{
    /// <summary>編集前の姿。新しく作るなら null。</summary>
    public ServerRoleDescriptor? Original { get; init; }

    public required string Name { get; init; }

    /// <summary>未指定なら空文字。サーバーが作成した利用者を当てる。</summary>
    public required string Owner { get; init; }

    /// <summary>このロールに入れるログインとロール。</summary>
    public IReadOnlyList<string> Members { get; init; } = [];

    /// <summary>このロールを入れる別のサーバー ロール。</summary>
    public IReadOnlyList<string> Memberships { get; init; } = [];

    public bool IsNew => Original is null;

    /// <summary>新規作成の初期値。SSMS と同じく所有者は空（実行した利用者）から始める。</summary>
    public static ServerRoleDraft ForNewRole() =>
        new()
        {
            Name = string.Empty,
            Owner = string.Empty
        };

    public static ServerRoleDraft FromDescriptor(ServerRoleDescriptor role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new ServerRoleDraft
        {
            Original = role,
            Name = role.Name.Value,
            Owner = role.Owner ?? string.Empty,
            Members = role.Members,
            Memberships = role.Memberships
        };
    }

    /// <summary>検証を通ったあとにだけ呼ぶこと。</summary>
    public ServerRoleDefinition ToDefinition()
    {
        var owner = Owner.Trim();

        return new ServerRoleDefinition(
            new RoleName(Name.Trim()),
            owner.Length > 0 ? owner : null)
        {
            Members = Members,
            Memberships = Memberships
        };
    }
}
