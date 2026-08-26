using SQLForge.Domain.Catalog;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// スキーマの追加・所有者の付け替え・削除の文面。スキーマ名も所有者も識別子なので、
/// <see cref="SqlServerIdentifier.Quote"/> を通して埋める。
/// </summary>
internal static class SqlServerSchemaStatements
{
    public static IReadOnlyList<string> Create(SchemaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var create = $"CREATE SCHEMA {Quote(definition.Name.Value)}";

        return
        [
            definition.Owner is { } owner ? $"{create} AUTHORIZATION {Quote(owner)};" : $"{create};"
        ];
    }

    /// <summary>
    /// スキーマは名前を変えられない（エンジンにその文面が無い）。
    /// 変えられるのは所有者だけで、空欄は「今のまま」として文面に出さない。
    /// </summary>
    public static IReadOnlyList<string> Alter(SchemaDescriptor original, SchemaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Owner is not { } owner
            || string.Equals(original.Owner, owner, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return [$"ALTER AUTHORIZATION ON SCHEMA::{Quote(original.Name.Value)} TO {Quote(owner)};"];
    }

    public static string Drop(SchemaName schema) => $"DROP SCHEMA {Quote(schema.Value)};";

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
