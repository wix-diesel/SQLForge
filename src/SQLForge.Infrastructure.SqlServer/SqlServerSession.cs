using System.Data.Common;
using System.Globalization;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// SQL Server 1 接続ぶんのセッション。カタログは sys スキーマのビューから読む。
///
/// SQL Server は 3 部名（db.sys.tables）で他のデータベースのカタログを読めるので、
/// データベースを切り替えるのに接続を張り直す必要がない。
/// </summary>
public sealed class SqlServerSession(ConnectionProfile profile, DbConnection connection, ServerInfo server)
    : AdoDatabaseSession(profile, connection, server)
{
    /// <summary>
    /// SSMS の「上位 N 行の選択」と同じ形。3 部名で書くので、
    /// あとから実行先のデータベースを変えても指す先が動かない。
    /// </summary>
    public override string BuildTableQuery(DatabaseName database, SchemaName schema, string table, int maxRows) =>
        $"SELECT TOP ({maxRows}) *\nFROM {Quote(database.Value)}.{Quote(schema.Value)}.{Quote(table)};";

    /// <summary>
    /// USE で切り替える。カタログの照会は 3 部名で読むのでこの状態に依らないが、
    /// エディタの文面は修飾なしで書かれるのが普通なので、実行の直前に合わせる。
    /// </summary>
    protected override async Task SwitchDatabaseAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"USE {Quote(database.Value)};";

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<IReadOnlyList<DatabaseDescriptor>> ReadDatabasesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerCatalogQueries.Databases;

        var databases = new List<DatabaseDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            databases.Add(new DatabaseDescriptor(
                new DatabaseName(reader.GetString(0)),
                IsSystem: reader.GetBoolean(1),
                IsAccessible: reader.GetBoolean(2),
                Collation: reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return databases;
    }

    protected override async Task<IReadOnlyList<SchemaDescriptor>> ReadSchemasAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerCatalogQueries.SchemasFormat, database);

        var schemas = new List<SchemaDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            schemas.Add(new SchemaDescriptor(new SchemaName(reader.GetString(0)), IsSystem: reader.GetBoolean(1)));
        }

        return schemas;
    }

    protected override async Task<IReadOnlyList<TableDescriptor>> ReadTablesAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerCatalogQueries.TablesFormat, database);

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@schema";
        parameter.Value = schema.Value;
        command.Parameters.Add(parameter);

        var tables = new List<TableDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tables.Add(new TableDescriptor(
                schema,
                reader.GetString(0),
                RowCount: reader.IsDBNull(1) ? null : reader.GetInt64(1)));
        }

        return tables;
    }

    /// <summary>データベース名だけは 3 部名として文面に埋める（識別子はパラメータにできない）。</summary>
    private static string Format(string queryFormat, DatabaseName database) =>
        string.Format(CultureInfo.InvariantCulture, queryFormat, Quote(database.Value));

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
