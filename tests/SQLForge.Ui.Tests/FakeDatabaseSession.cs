using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Editing;
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
    private readonly Dictionary<string, IReadOnlyList<DatabaseRoleDescriptor>> _databaseRoles =
        new(StringComparer.Ordinal);

    public FakeDatabaseSession WithDatabaseUsers(string database, params DatabaseUserDescriptor[] users)
    {
        _databaseUsers[database] = users;
        return this;
    }

    /// <summary>名前だけのロール。所有者もメンバーも見ないテストではこちらを使う。</summary>
    public FakeDatabaseSession WithDatabaseRoles(string database, params string[] roles)
    {
        _databaseRoles[database] = roles.Select(role => new DatabaseRoleDescriptor(new RoleName(role))).ToList();
        return this;
    }

    public FakeDatabaseSession WithDatabaseRoles(string database, params DatabaseRoleDescriptor[] roles)
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

    public int DatabaseRoleCallCount { get; private set; }

    public Task<IReadOnlyList<DatabaseRoleDescriptor>> ListDatabaseRolesAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        DatabaseRoleCallCount++;

        return SecurityFailure is not null
            ? Task.FromException<IReadOnlyList<DatabaseRoleDescriptor>>(SecurityFailure)
            : Task.FromResult(_databaseRoles.TryGetValue(database.Value, out var roles) ? roles : []);
    }

    public DatabaseRoleDefinition? CreatedDatabaseRole { get; private set; }

    public DatabaseRoleDescriptor? AlteredOriginalDatabaseRole { get; private set; }

    public DatabaseRoleDefinition? AlteredDatabaseRole { get; private set; }

    public RoleName? DroppedDatabaseRole { get; private set; }

    public Task CreateDatabaseRoleAsync(
        DatabaseName database,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        CreatedDatabaseRole = definition;

        return Task.CompletedTask;
    }

    public Task AlterDatabaseRoleAsync(
        DatabaseName database,
        DatabaseRoleDescriptor original,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        AlteredOriginalDatabaseRole = original;
        AlteredDatabaseRole = definition;

        return Task.CompletedTask;
    }

    public Task DropDatabaseRoleAsync(
        DatabaseName database,
        RoleName role,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        DroppedDatabaseRole = role;

        return Task.CompletedTask;
    }

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
    private IReadOnlyList<ServerRoleDescriptor> _serverRoles = [];

    public FakeDatabaseSession WithServerLogins(params ServerLoginDescriptor[] logins)
    {
        _serverLogins = logins;
        return this;
    }

    /// <summary>名前だけのロール。所有者もメンバーも見ないテストではこちらを使う。</summary>
    public FakeDatabaseSession WithServerRoles(params string[] roles)
    {
        _serverRoles = roles.Select(role => new ServerRoleDescriptor(new RoleName(role))).ToList();
        return this;
    }

    public FakeDatabaseSession WithServerRoles(params ServerRoleDescriptor[] roles)
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

    public int ServerRoleCallCount { get; private set; }

    public Task<IReadOnlyList<ServerRoleDescriptor>> ListServerRolesAsync(
        CancellationToken cancellationToken = default)
    {
        ServerRoleCallCount++;

        return SecurityFailure is not null
            ? Task.FromException<IReadOnlyList<ServerRoleDescriptor>>(SecurityFailure)
            : Task.FromResult(_serverRoles);
    }

    public ServerRoleDefinition? CreatedServerRole { get; private set; }

    public ServerRoleDescriptor? AlteredOriginalServerRole { get; private set; }

    public ServerRoleDefinition? AlteredServerRole { get; private set; }

    public RoleName? DroppedServerRole { get; private set; }

    public Task CreateServerRoleAsync(
        ServerRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        CreatedServerRole = definition;

        return Task.CompletedTask;
    }

    public Task AlterServerRoleAsync(
        ServerRoleDescriptor original,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        AlteredOriginalServerRole = original;
        AlteredServerRole = definition;

        return Task.CompletedTask;
    }

    public Task DropServerRoleAsync(RoleName role, CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        DroppedServerRole = role;

        return Task.CompletedTask;
    }

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

    private readonly List<LoginUserMapping> _mappings = [];
    private readonly Dictionary<string, IReadOnlyList<PermissionEntry>> _permissions = new(StringComparer.Ordinal);
    private readonly Dictionary<SecurableKind, IReadOnlyList<SecurableReference>> _securables = [];

    public SchemaDefinition? CreatedSchema { get; private set; }

    public SchemaDescriptor? AlteredOriginalSchema { get; private set; }

    public SchemaDefinition? AlteredSchema { get; private set; }

    public SchemaName? DroppedSchema { get; private set; }

    public Task CreateSchemaAsync(
        DatabaseName database,
        SchemaDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        CreatedSchema = definition;

        return Task.CompletedTask;
    }

    public Task AlterSchemaAsync(
        DatabaseName database,
        SchemaDescriptor original,
        SchemaDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        AlteredOriginalSchema = original;
        AlteredSchema = definition;

        return Task.CompletedTask;
    }

    public Task DropSchemaAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        DroppedSchema = schema;

        return Task.CompletedTask;
    }

    public FakeDatabaseSession WithLoginUserMappings(params LoginUserMapping[] mappings)
    {
        _mappings.Clear();
        _mappings.AddRange(mappings);

        return this;
    }

    public IReadOnlyList<LoginUserMapping>? AppliedOriginalMappings { get; private set; }

    public IReadOnlyList<LoginUserMapping>? AppliedMappings { get; private set; }

    public Task<IReadOnlyList<LoginUserMapping>> ListLoginUserMappingsAsync(
        ServerLoginName login,
        CancellationToken cancellationToken = default) =>
        SecurityFailure is not null
            ? Task.FromException<IReadOnlyList<LoginUserMapping>>(SecurityFailure)
            : Task.FromResult<IReadOnlyList<LoginUserMapping>>(_mappings);

    public Task ApplyLoginUserMappingsAsync(
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        AppliedOriginalMappings = original;
        AppliedMappings = desired;

        return Task.CompletedTask;
    }

    public FakeDatabaseSession WithPermissions(string principal, params PermissionEntry[] entries)
    {
        _permissions[principal] = entries;
        return this;
    }

    public FakeDatabaseSession WithSecurables(SecurableKind kind, params SecurableReference[] securables)
    {
        _securables[kind] = securables;
        return this;
    }

    public SecurityPrincipal? AppliedPrincipal { get; private set; }

    public IReadOnlyList<PermissionEntry>? AppliedOriginalPermissions { get; private set; }

    public IReadOnlyList<PermissionEntry>? AppliedPermissions { get; private set; }

    public Task<IReadOnlyList<PermissionEntry>> ListPermissionsAsync(
        SecurityPrincipal principal,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default) =>
        SecurityFailure is not null
            ? Task.FromException<IReadOnlyList<PermissionEntry>>(SecurityFailure)
            : Task.FromResult(_permissions.TryGetValue(principal.Name, out var entries) ? entries : []);

    public Task ApplyPermissionsAsync(
        SecurityPrincipal principal,
        DatabaseName? database,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired,
        CancellationToken cancellationToken = default)
    {
        if (SecurityFailure is not null)
        {
            return Task.FromException(SecurityFailure);
        }

        AppliedPrincipal = principal;
        AppliedOriginalPermissions = original;
        AppliedPermissions = desired;

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SecurableReference>> ListSecurablesAsync(
        SecurableKind kind,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default) =>
        SecurityFailure is not null
            ? Task.FromException<IReadOnlyList<SecurableReference>>(SecurityFailure)
            : Task.FromResult(_securables.TryGetValue(kind, out var securables) ? securables : []);

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

    private readonly Dictionary<string, EditableRowSet> _editableRows = new(StringComparer.Ordinal);

    /// <summary>編集グリッドが読む行。差し替えてグリッドの見え方を確かめる。</summary>
    public FakeDatabaseSession WithEditableRows(string database, string schema, string table, EditableRowSet rows)
    {
        _editableRows[$"{database}.{schema}.{table}"] = rows;
        return this;
    }

    /// <summary>編集グリッドの読み書きで投げる例外。失敗の見え方を確かめるために差し込む。</summary>
    public Exception? EditFailure { get; set; }

    /// <summary>更新が返す行数。0 にすると「行が見つからない」を作れる。</summary>
    public int UpdatedRows { get; set; } = 1;

    public int EditableRowsCallCount { get; private set; }

    public int EditableMaxRows { get; private set; }

    public string? UpdatedTable { get; private set; }

    public TableCellUpdate? LastUpdate { get; private set; }

    public Task<EditableRowSet> ReadEditableRowsAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        EditableRowsCallCount++;
        EditableMaxRows = maxRows;

        if (EditFailure is not null)
        {
            return Task.FromException<EditableRowSet>(EditFailure);
        }

        return Task.FromResult(
            _editableRows.TryGetValue($"{database.Value}.{schema.Value}.{table}", out var rows)
                ? rows
                : new EditableRowSet([], []));
    }

    public Task<int> UpdateTableCellAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        TableCellUpdate update,
        CancellationToken cancellationToken = default)
    {
        UpdatedTable = $"{database.Value}.{schema.Value}.{table}";
        LastUpdate = update;

        return EditFailure is not null ? Task.FromException<int>(EditFailure) : Task.FromResult(UpdatedRows);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
