using System.Data.Common;
using SQLForge.Domain.Catalog;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// スキーマの追加・所有者の付け替え・削除の受け持ち。読み取り（一覧）はカタログの側にある。
///
/// ユーザーと同じく 3 部名では書けない（CREATE SCHEMA は今いるデータベースにしか作れない）ので、
/// 実行の前にデータベースを切り替える。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task CreateSchemaAsync(
        DatabaseName database,
        SchemaDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => CreateSchemaAsync(connection, database, definition, token),
            cancellationToken);
    }

    public Task AlterSchemaAsync(
        DatabaseName database,
        SchemaDescriptor original,
        SchemaDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => AlterSchemaAsync(connection, database, original, definition, token),
            cancellationToken);
    }

    public Task DropSchemaAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default) =>
        WriteAsync((connection, token) => DropSchemaAsync(connection, database, schema, token), cancellationToken);

    protected abstract Task CreateSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task AlterSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDescriptor original,
        SchemaDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task DropSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken);
}
