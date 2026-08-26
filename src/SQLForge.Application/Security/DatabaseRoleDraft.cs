using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// データベース ロールのプロパティ ダイアログで編集中の入力値。
/// エンティティ（<see cref="DatabaseRoleDefinition"/>）は常に妥当である前提なので、
/// まだ妥当とは限らない入力はこの器で受け渡す。
/// </summary>
public sealed record DatabaseRoleDraft
{
    /// <summary>編集前の姿。新しく作るなら null。</summary>
    public DatabaseRoleDescriptor? Original { get; init; }

    public required string Name { get; init; }

    /// <summary>未指定なら空文字。サーバーが作成した利用者を当てる。</summary>
    public required string Owner { get; init; }

    /// <summary>このロールに入れるユーザーとロール。</summary>
    public IReadOnlyList<string> Members { get; init; } = [];

    /// <summary>このロールに持たせるスキーマ。</summary>
    public IReadOnlyList<string> OwnedSchemas { get; init; } = [];

    public bool IsNew => Original is null;

    /// <summary>新規作成の初期値。SSMS と同じく所有者は空（実行した利用者）から始める。</summary>
    public static DatabaseRoleDraft ForNewRole() =>
        new()
        {
            Name = string.Empty,
            Owner = string.Empty
        };

    public static DatabaseRoleDraft FromDescriptor(DatabaseRoleDescriptor role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new DatabaseRoleDraft
        {
            Original = role,
            Name = role.Name.Value,
            Owner = role.Owner ?? string.Empty,
            Members = role.Members,
            OwnedSchemas = role.OwnedSchemas
        };
    }

    /// <summary>検証を通ったあとにだけ呼ぶこと。</summary>
    public DatabaseRoleDefinition ToDefinition()
    {
        var owner = Owner.Trim();

        return new DatabaseRoleDefinition(
            new RoleName(Name.Trim()),
            owner.Length > 0 ? owner : null)
        {
            Members = Members,
            OwnedSchemas = OwnedSchemas
        };
    }
}
