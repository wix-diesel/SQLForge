using System.Text;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// データベース ロールの追加・編集・削除の文面。ロール名・所有者・メンバー・スキーマ名は
/// どれも識別子でパラメータにできないので、<see cref="SqlServerIdentifier.Quote"/> を通して埋める。
/// </summary>
internal static class SqlServerDatabaseRoleStatements
{
    /// <summary>
    /// 所有を外したスキーマの行き先。スキーマは持ち主を空にできないので、
    /// SSMS と同じく dbo へ移す。
    /// </summary>
    private const string DefaultSchemaOwner = "dbo";

    public static IReadOnlyList<string> Create(DatabaseRoleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var name = Quote(definition.Name.Value);
        var create = new StringBuilder("CREATE ROLE ").Append(name);

        if (definition.Owner is { } owner)
        {
            create.Append($" AUTHORIZATION {Quote(owner)}");
        }

        var statements = new List<string> { create.Append(';').ToString() };

        statements.AddRange(definition.Members.Select(member => AddMember(name, member)));
        statements.AddRange(definition.OwnedSchemas.Select(schema => OwnSchema(schema, name)));

        return statements;
    }

    public static IReadOnlyList<string> Alter(DatabaseRoleDescriptor original, DatabaseRoleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        var statements = new List<string>();

        // 名前を先に変える。以降のメンバー操作と所有権の移動は新しい名前で行う。
        if (!string.Equals(original.Name.Value, definition.Name.Value, StringComparison.Ordinal))
        {
            statements.Add(
                $"ALTER ROLE {Quote(original.Name.Value)} WITH NAME = {Quote(definition.Name.Value)};");
        }

        var name = Quote(definition.Name.Value);

        // 所有者は空にできない。空欄は「今のまま」として文面に出さない。
        if (definition.Owner is { } owner
            && !string.Equals(original.Owner, owner, StringComparison.OrdinalIgnoreCase))
        {
            statements.Add($"ALTER AUTHORIZATION ON ROLE::{name} TO {Quote(owner)};");
        }

        statements.AddRange(definition.Members
            .Except(original.Members, StringComparer.OrdinalIgnoreCase)
            .Select(member => AddMember(name, member)));

        statements.AddRange(original.Members
            .Except(definition.Members, StringComparer.OrdinalIgnoreCase)
            .Select(member => $"ALTER ROLE {name} DROP MEMBER {Quote(member)};"));

        statements.AddRange(definition.OwnedSchemas
            .Except(original.OwnedSchemas, StringComparer.OrdinalIgnoreCase)
            .Select(schema => OwnSchema(schema, name)));

        statements.AddRange(original.OwnedSchemas
            .Except(definition.OwnedSchemas, StringComparer.OrdinalIgnoreCase)
            .Select(schema => OwnSchema(schema, Quote(DefaultSchemaOwner))));

        return statements;
    }

    public static string Drop(RoleName role) => $"DROP ROLE {Quote(role.Value)};";

    private static string AddMember(string quotedRole, string member) =>
        $"ALTER ROLE {quotedRole} ADD MEMBER {Quote(member)};";

    private static string OwnSchema(string schema, string quotedOwner) =>
        $"ALTER AUTHORIZATION ON SCHEMA::{Quote(schema)} TO {quotedOwner};";

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
