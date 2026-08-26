using System.Data.Common;
using SQLForge.Domain.Catalog;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// スキーマの追加・所有者の付け替え・削除の受け持ち。読み取り（一覧）はカタログの側にある。
///
/// CREATE SCHEMA は今いるデータベースにしか作れないので、ユーザーやロールと同じく
/// 実行の前にデータベースを切り替える。
/// </summary>
public sealed partial class SqlServerSession
{
    protected override Task CreateSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(connection, database, SqlServerSchemaStatements.Create(definition), cancellationToken);

    protected override Task AlterSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDescriptor original,
        SchemaDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(
            connection, database, SqlServerSchemaStatements.Alter(original, definition), cancellationToken);

    protected override Task DropSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(connection, database, [SqlServerSchemaStatements.Drop(schema)], cancellationToken);
}
