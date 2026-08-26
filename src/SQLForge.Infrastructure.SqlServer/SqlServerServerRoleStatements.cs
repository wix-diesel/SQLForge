using System.Text;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// サーバー ロールの追加・編集・削除の文面。ロール名・所有者・メンバー名は
/// どれも識別子でパラメータにできないので、<see cref="SqlServerIdentifier.Quote"/> を通して埋める。
/// </summary>
internal static class SqlServerServerRoleStatements
{
    public static IReadOnlyList<string> Create(ServerRoleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var name = Quote(definition.Name.Value);
        var create = new StringBuilder("CREATE SERVER ROLE ").Append(name);

        if (definition.Owner is { } owner)
        {
            create.Append($" AUTHORIZATION {Quote(owner)}");
        }

        var statements = new List<string> { create.Append(';').ToString() };

        statements.AddRange(definition.Members.Select(member => AddMember(name, Quote(member))));
        statements.AddRange(definition.Memberships.Select(role => AddMember(Quote(role), name)));

        return statements;
    }

    public static IReadOnlyList<string> Alter(ServerRoleDescriptor original, ServerRoleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        var statements = new List<string>();

        // 名前を先に変える。以降のメンバー操作は新しい名前で行う。
        if (!string.Equals(original.Name.Value, definition.Name.Value, StringComparison.Ordinal))
        {
            statements.Add(
                $"ALTER SERVER ROLE {Quote(original.Name.Value)} WITH NAME = {Quote(definition.Name.Value)};");
        }

        var name = Quote(definition.Name.Value);

        // 所有者は空にできない。空欄は「今のまま」として文面に出さない。
        if (definition.Owner is { } owner
            && !string.Equals(original.Owner, owner, StringComparison.OrdinalIgnoreCase))
        {
            statements.Add($"ALTER AUTHORIZATION ON SERVER ROLE::{name} TO {Quote(owner)};");
        }

        statements.AddRange(definition.Members
            .Except(original.Members, StringComparer.OrdinalIgnoreCase)
            .Select(member => AddMember(name, Quote(member))));

        statements.AddRange(original.Members
            .Except(definition.Members, StringComparer.OrdinalIgnoreCase)
            .Select(member => DropMember(name, Quote(member))));

        statements.AddRange(definition.Memberships
            .Except(original.Memberships, StringComparer.OrdinalIgnoreCase)
            .Select(role => AddMember(Quote(role), name)));

        statements.AddRange(original.Memberships
            .Except(definition.Memberships, StringComparer.OrdinalIgnoreCase)
            .Select(role => DropMember(Quote(role), name)));

        return statements;
    }

    public static string Drop(RoleName role) => $"DROP SERVER ROLE {Quote(role.Value)};";

    private static string AddMember(string quotedRole, string quotedMember) =>
        $"ALTER SERVER ROLE {quotedRole} ADD MEMBER {quotedMember};";

    private static string DropMember(string quotedRole, string quotedMember) =>
        $"ALTER SERVER ROLE {quotedRole} DROP MEMBER {quotedMember};";

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
