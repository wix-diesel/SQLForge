using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;
using SQLForge.Domain.Security;
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
    private readonly Dictionary<string, IReadOnlyList<ColumnDescriptor>> _columns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<StoredProcedureDescriptor>> _storedProcedures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<StoredProcedureParameterDescriptor>> _storedProcedureParameters = new(StringComparer.Ordinal);

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

    public FakeDatabaseSession WithColumns(string database, string schema, string table, params ColumnDescriptor[] columns)
    {
        _columns[$"{database}.{schema}.{table}"] = columns;
        return this;
    }

    public FakeDatabaseSession WithStoredProcedures(
        string database, string schema, params StoredProcedureDescriptor[] procedures)
    {
        _storedProcedures[$"{database}.{schema}"] = procedures;
        return this;
    }

    public FakeDatabaseSession WithStoredProcedureParameters(
        string database, string schema, string procedure, params StoredProcedureParameterDescriptor[] parameters)
    {
        _storedProcedureParameters[$"{database}.{schema}.{procedure}"] = parameters;
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

    public Task<IReadOnlyList<ColumnDescriptor>> ListColumnsAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_columns.TryGetValue($"{database.Value}.{schema.Value}.{table}", out var columns) ? columns : []);

    public Task<IReadOnlyList<StoredProcedureDescriptor>> ListStoredProceduresAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            _storedProcedures.TryGetValue($"{database.Value}.{schema.Value}", out var procedures) ? procedures : []);

    public Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ListStoredProcedureParametersAsync(
        DatabaseName database,
        SchemaName schema,
        string procedure,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            _storedProcedureParameters.TryGetValue($"{database.Value}.{schema.Value}.{procedure}", out var parameters)
                ? parameters
                : []);

    private readonly Dictionary<string, IReadOnlyList<DatabaseUserDescriptor>> _databaseUsers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _databaseRoles = new(StringComparer.Ordinal);

    public FakeDatabaseSession WithDatabaseUsers(string database, params DatabaseUserDescriptor[] users)
    {
        _databaseUsers[database] = users;
        return this;
    }

    public FakeDatabaseSession WithDatabaseRoles(string database, params string[] roles)
    {
        _databaseRoles[database] = roles;
        return this;
    }

    /// <summary>ユーザーの読み書きで投げる例外。権限不足の見え方を確かめるために差し込む。</summary>
    public Exception? SecurityFailure { get; set; }

    public string? CreatedUserDatabase { get; private set; }

    public DatabaseUserDefinition? CreatedUser { get; private set; }

    public DatabaseUserDescriptor? AlteredOriginal { get; private set; }

    public DatabaseUserDefinition? AlteredUser { get; private set; }

    public string? DroppedUserDatabase { get; private set; }

    public DatabaseUserName? DroppedUser { get; private set; }

    public int DatabaseUserCallCount { get; private set; }

    public Task<IReadOnlyList<DatabaseUserDescriptor>> ListDatabaseUsersAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        DatabaseUserCallCount++;

        return SecurityFailure is not null
            ? Task.FromException<IReadOnlyList<DatabaseUserDescriptor>>(SecurityFailure)
            : Task.FromResult(_databaseUsers.TryGetValue(database.Value, out var users) ? users : []);
    }

    public Task<IReadOnlyList<string>> ListDatabaseRolesAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_databaseRoles.TryGetValue(database.Value, out var roles) ? roles : []);

    public Task CreateDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        CreatedUserDatabase = database.Value;
        CreatedUser = definition;

        return Task.CompletedTask;
    }

    public Task AlterDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserDescriptor original,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        AlteredOriginal = original;
        AlteredUser = definition;

        return Task.CompletedTask;
    }

    public Task DropDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserName user,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        DroppedUserDatabase = database.Value;
        DroppedUser = user;

        return Task.CompletedTask;
    }

    private IReadOnlyList<ServerLoginDescriptor> _serverLogins = [];
    private IReadOnlyList<string> _serverRoles = [];

    public FakeDatabaseSession WithServerLogins(params ServerLoginDescriptor[] logins)
    {
        _serverLogins = logins;
        return this;
    }

    public FakeDatabaseSession WithServerRoles(params string[] roles)
    {
        _serverRoles = roles;
        return this;
    }

    public ServerLoginDefinition? CreatedLogin { get; private set; }

    public ServerLoginDescriptor? AlteredOriginalLogin { get; private set; }

    public ServerLoginDefinition? AlteredLogin { get; private set; }

    public ServerLoginName? DroppedLogin { get; private set; }

    public int ServerLoginCallCount { get; private set; }

    public Task<IReadOnlyList<ServerLoginDescriptor>> ListServerLoginsAsync(
        CancellationToken cancellationToken = default)
    {
        ServerLoginCallCount++;

        return SecurityFailure is not null
            ? Task.FromException<IReadOnlyList<ServerLoginDescriptor>>(SecurityFailure)
            : Task.FromResult(_serverLogins);
    }

    public Task<IReadOnlyList<string>> ListServerRolesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_serverRoles);

    public Task CreateServerLoginAsync(
        ServerLoginDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        CreatedLogin = definition;

        return Task.CompletedTask;
    }

    public Task AlterServerLoginAsync(
        ServerLoginDescriptor original,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        AlteredOriginalLogin = original;
        AlteredLogin = definition;

        return Task.CompletedTask;
    }

    public Task DropServerLoginAsync(ServerLoginName login, CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        DroppedLogin = login;

        return Task.CompletedTask;
    }

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
