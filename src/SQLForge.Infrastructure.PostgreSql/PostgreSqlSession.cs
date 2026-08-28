using System.Data.Common;
using System.Globalization;
using Npgsql;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;

namespace SQLForge.Infrastructure.PostgreSql;

/// <summary>
/// PostgreSQL 1 接続ぶんのセッション。カタログは pg_catalog のビューから読む。
///
/// SQL Server との一番の違いは、1 本の接続で他のデータベースを覗けないこと。
/// 3 部名（db.sys.tables）に当たる書き方が無いので、別のデータベースを読むときは
/// 接続をそのデータベースへ張り直す。<see cref="Domain.Catalog.DatabaseDescriptor"/> を
/// 読む pg_database だけは共有カタログなので、どこに繋いでいても読める。
/// </summary>
public sealed partial class PostgreSqlSession : AdoDatabaseSession
{
    private readonly string _connectionString;

    /// <param name="connectionString">
    /// この接続を開くのに使った接続文字列。データベースを張り直すときに、
    /// データベース名だけを差し替えて開き直すために持っておく
    /// （<see cref="DbConnection.ConnectionString"/> から読み直すと、
    /// Npgsql がパスワードを伏せて返すので使えない）。
    /// </param>
    public PostgreSqlSession(
        ConnectionProfile profile,
        DbConnection connection,
        ServerInfo server,
        string connectionString,
        IAsyncDisposable? route = null)
        : base(profile, connection, server, route)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// この版の PostgreSQL ドライバーはカタログを読むところまで。
    /// セキュリティと編集グリッドはまだ書いていないので、画面にも出さない。
    /// </summary>
    public override SessionCapabilities Capabilities => SessionCapabilities.CatalogOnly;

    /// <summary>
    /// 先頭 N 行をのぞく文面。PostgreSQL に <c>TOP</c> は無いので、末尾の <c>LIMIT</c> で絞る。
    /// 識別子は二重引用符で囲む（囲まないと小文字へ畳まれ、別の名前を指してしまう）。
    /// </summary>
    public override string BuildTopRowsQuery(SchemaName schema, string table, int maxRows) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"SELECT * FROM {Quote(schema.Value)}.{Quote(table)} LIMIT {maxRows};");

    /// <summary>
    /// 実行先のデータベースへ移る。PostgreSQL の接続は開いたときのデータベースに
    /// 縛られるので、閉じて開き直すしかない（SQL Server の USE に当たる文が無い）。
    /// </summary>
    protected override async Task SwitchDatabaseAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        if (string.Equals(connection.Database, database.Value, StringComparison.Ordinal))
        {
            return;
        }

        // Npgsql の ChangeDatabase も中でやっていることは同じだが、同期でしか呼べない。
        // 開き直しはサーバーとの往復なので、待つ側を止めないよう非同期で組む。
        await connection.CloseAsync().ConfigureAwait(false);
        connection.ConnectionString = ConnectionStringFor(database);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<IReadOnlyList<DatabaseDescriptor>> ReadDatabasesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        // pg_database は共有カタログなので、今どのデータベースに居ても同じものが読める。
        await using var command = Command(connection, PostgreSqlCatalogQueries.Databases);

        var databases = new List<DatabaseDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            databases.Add(new DatabaseDescriptor(
                new DatabaseName(reader.GetString(0)),
                IsSystem: reader.GetBoolean(1),
                IsAccessible: reader.GetBoolean(2),
                Collation: reader.IsDBNull(3) ? null : reader.GetString(3),

                // PostgreSQL はデータベースの作成日時を持たない。
                CreatedAt: null));
        }

        return databases;
    }

    protected override async Task<IReadOnlyList<SchemaDescriptor>> ReadSchemasAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        await SwitchDatabaseAsync(connection, database, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, PostgreSqlCatalogQueries.Schemas);

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
        await SwitchDatabaseAsync(connection, database, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, PostgreSqlCatalogQueries.Tables, ("@schema", schema.Value));

        var tables = new List<TableDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tables.Add(new TableDescriptor(
                schema,
                reader.GetString(0),
                RowCount: reader.IsDBNull(1) ? null : reader.GetInt64(1),
                CreatedAt: null));
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
        await SwitchDatabaseAsync(connection, database, cancellationToken).ConfigureAwait(false);

        await using var command = Command(
            connection,
            PostgreSqlCatalogQueries.Columns,
            ("@schema", schema.Value),
            ("@table", table));

        var columns = new List<ColumnDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new ColumnDescriptor(
                reader.GetString(0),
                reader.GetInt32(1),

                // format_type() が numeric(18,2) のような表示用の形まで組んでくれる。
                reader.GetString(2),
                IsNullable: reader.GetBoolean(3),
                IsIdentity: reader.GetBoolean(4),
                IsPrimaryKey: reader.GetBoolean(5)));
        }

        return columns;
    }

    protected override async Task<IReadOnlyList<StoredProcedureDescriptor>> ReadStoredProceduresAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken)
    {
        await SwitchDatabaseAsync(connection, database, cancellationToken).ConfigureAwait(false);

        await using var command = Command(
            connection,
            PostgreSqlCatalogQueries.StoredProcedures,
            ("@schema", schema.Value));

        var procedures = new List<StoredProcedureDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            procedures.Add(new StoredProcedureDescriptor(
                schema,
                reader.GetString(0),
                ParameterCount: reader.GetInt32(1),
                CreatedAt: null));
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
        await SwitchDatabaseAsync(connection, database, cancellationToken).ConfigureAwait(false);

        await using var command = Command(
            connection,
            PostgreSqlCatalogQueries.StoredProcedureParameters,
            ("@schema", schema.Value),
            ("@procedure", procedure));

        var parameters = new List<StoredProcedureParameterDescriptor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            parameters.Add(new StoredProcedureParameterDescriptor(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                IsOutput: reader.GetBoolean(3),
                HasDefaultValue: reader.GetBoolean(4)));
        }

        return parameters;
    }

    private static string Quote(string identifier) => PostgreSqlIdentifier.Quote(identifier);

    /// <summary>データベース名だけを差し替えた接続文字列。開き直すときに使う。</summary>
    private string ConnectionStringFor(DatabaseName database) =>
        new NpgsqlConnectionStringBuilder(_connectionString) { Database = database.Value }.ConnectionString;

    /// <summary>文面とパラメータから命令を 1 つ組む。値は必ずパラメータで渡す。</summary>
    private static DbCommand Command(
        DbConnection connection,
        string text,
        params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = text;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
