using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;
using SQLForge.Infrastructure.Connections;

namespace SQLForge.Ui.Tests;

/// <summary>
/// カタログの中身を決め打ちで返すセッション。実サーバーなしで、
/// 並べ替えとツリーの遅延読み込みだけを確かめるために使う。
/// </summary>
public sealed class FakeDatabaseSession : IDatabaseSession
{
    private readonly Dictionary<string, IReadOnlyList<SchemaDescriptor>> _schemas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<TableDescriptor>> _tables = new(StringComparer.Ordinal);

    public FakeDatabaseSession(ConnectionProfile? profile = null)
    {
        Profile = profile ?? SeedConnections.Create().First();
        Server = new ServerInfo("SQL Server 2022", "16.0.4215.2", "Developer Edition", IsEncrypted: true);
    }

    public ConnectionProfile Profile { get; }

    public ServerInfo Server { get; }

    public IReadOnlyList<DatabaseDescriptor> Databases { get; set; } = [];

    /// <summary>読み込み時に投げる例外。失敗の見え方を確かめるために差し込む。</summary>
    public Exception? Failure { get; set; }

    public int DatabaseCallCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public FakeDatabaseSession WithSchemas(string database, params SchemaDescriptor[] schemas)
    {
        _schemas[database] = schemas;
        return this;
    }

    public FakeDatabaseSession WithTables(string database, string schema, params TableDescriptor[] tables)
    {
        _tables[$"{database}.{schema}"] = tables;
        return this;
    }

    public Task<IReadOnlyList<DatabaseDescriptor>> ListDatabasesAsync(CancellationToken cancellationToken = default)
    {
        DatabaseCallCount++;

        return Failure is not null
            ? Task.FromException<IReadOnlyList<DatabaseDescriptor>>(Failure)
            : Task.FromResult(Databases);
    }

    public Task<IReadOnlyList<SchemaDescriptor>> ListSchemasAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_schemas.TryGetValue(database.Value, out var schemas) ? schemas : []);

    public Task<IReadOnlyList<TableDescriptor>> ListTablesAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_tables.TryGetValue($"{database.Value}.{schema.Value}", out var tables) ? tables : []);

    /// <summary>実行で返す結果。差し替えて結果ペインの見え方を確かめる。</summary>
    public QueryResult? NextResult { get; set; }

    /// <summary>実行時に投げる例外。失敗の見え方を確かめるために差し込む。</summary>
    public Exception? QueryFailure { get; set; }

    public string? ExecutedSql { get; private set; }

    public string? ExecutedDatabase { get; private set; }

    public int ExecutedMaxRows { get; private set; }

    /// <summary>置くと、合図をもらうまで実行が返らなくなる。実行中の割り込みを作るのに使う。</summary>
    public TaskCompletionSource? QueryGate { get; set; }

    public async Task<QueryResult> ExecuteQueryAsync(
        DatabaseName database,
        string sql,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        ExecutedDatabase = database.Value;
        ExecutedSql = sql;
        ExecutedMaxRows = maxRows;

        if (QueryGate is { } gate)
        {
            await gate.Task.ConfigureAwait(false);
        }

        return QueryFailure is not null
            ? throw QueryFailure
            : NextResult ?? new QueryResult([], -1, TimeSpan.Zero);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
