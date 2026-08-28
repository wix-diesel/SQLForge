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
public sealed partial class SqlServerSession(
    ConnectionProfile profile,
    DbConnection connection,
    ServerInfo server,
    IAsyncDisposable? route = null)
    : AdoDatabaseSession(profile, connection, server, route)
{
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
                Collation: reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt: reader.IsDBNull(4) ? null : reader.GetDateTime(4)));
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
            schemas.Add(new SchemaDescriptor(
                new SchemaName(reader.GetString(0)),
                IsSystem: reader.GetBoolean(1),
                Owner: reader.IsDBNull(2) ? null : reader.GetString(2)));
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
                RowCount: reader.IsDBNull(1) ? null : reader.GetInt64(1),
                CreatedAt: reader.IsDBNull(2) ? null : reader.GetDateTime(2)));
        }

        return tables;
    }

    protected override async Task<IReadOnlyList<ColumnDescriptor>> ReadColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerCatalogQueries.ColumnsFormat, database);

        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "@schema";
        schemaParameter.Value = schema.Value;
        command.Parameters.Add(schemaParameter);

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@table";
        tableParameter.Value = table;
        command.Parameters.Add(tableParameter);

        var columns = new List<ColumnDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var dataType = SqlServerTypeFormat.Describe(
                reader.GetString(2),
                reader.GetInt16(3),
                reader.GetByte(4),
                reader.GetByte(5));

            columns.Add(new ColumnDescriptor(
                reader.GetString(0),
                reader.GetInt32(1),
                dataType,
                IsNullable: reader.GetBoolean(6),
                IsIdentity: reader.GetBoolean(7),
                IsPrimaryKey: reader.GetBoolean(8)));
        }

        return columns;
    }

    protected override async Task<IReadOnlyList<StoredProcedureDescriptor>> ReadStoredProceduresAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerCatalogQueries.StoredProceduresFormat, database);

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@schema";
        parameter.Value = schema.Value;
        command.Parameters.Add(parameter);

        var procedures = new List<StoredProcedureDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            procedures.Add(new StoredProcedureDescriptor(
                schema,
                reader.GetString(0),
                ParameterCount: reader.GetInt32(1),
                CreatedAt: reader.IsDBNull(2) ? null : reader.GetDateTime(2)));
        }

        return procedures;
    }

    protected override async Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ReadStoredProcedureParametersAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string procedure,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerCatalogQueries.StoredProcedureParametersFormat, database);

        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "@schema";
        schemaParameter.Value = schema.Value;
        command.Parameters.Add(schemaParameter);

        var procedureParameter = command.CreateParameter();
        procedureParameter.ParameterName = "@procedure";
        procedureParameter.Value = procedure;
        command.Parameters.Add(procedureParameter);

        var parameters = new List<StoredProcedureParameterDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var dataType = SqlServerTypeFormat.Describe(
                reader.GetString(2),
                reader.GetInt16(3),
                reader.GetByte(4),
                reader.GetByte(5));

            parameters.Add(new StoredProcedureParameterDescriptor(
                reader.GetString(0),
                reader.GetInt32(1),
                dataType,
                IsOutput: reader.GetBoolean(6),
                HasDefaultValue: reader.GetBoolean(7)));
        }

        return parameters;
    }

    /// <summary>データベース名だけは 3 部名として文面に埋める（識別子はパラメータにできない）。</summary>
    private static string Format(string queryFormat, DatabaseName database) =>
        string.Format(CultureInfo.InvariantCulture, queryFormat, Quote(database.Value));

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
