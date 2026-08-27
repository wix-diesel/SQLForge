using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Editing;
using SQLForge.Domain.Security;
using SQLForge.Infrastructure.Connections;

namespace SQLForge.Ui.Tests;

/// <summary>
/// <see cref="AdoDatabaseSession"/> の口をすべて空で埋めたセッション。
///
/// 共通部分（門の作法・トランザクション・行の読み取り）を試すテストは、そのうち 1 つ 2 つの
/// 口しか使わない。ここで既定を用意しておけば、テストは見たい口だけを上書きすればよく、
/// ポートが増えるたびに差し込み側を 2 つ 3 つと直して回らずに済む。
/// </summary>
internal abstract class StubAdoSession(DbConnection connection)
    : AdoDatabaseSession(
        SeedConnections.Create().First(),
        connection,
        new ServerInfo("SQL Server 2022", "16.0.4215.2"))
{
    protected override Task SwitchDatabaseAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<IReadOnlyList<DatabaseDescriptor>> ReadDatabasesAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DatabaseDescriptor>>([]);

    protected override Task<IReadOnlyList<SchemaDescriptor>> ReadSchemasAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SchemaDescriptor>>([]);

    protected override Task<IReadOnlyList<TableDescriptor>> ReadTablesAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TableDescriptor>>([]);

    protected override Task<IReadOnlyList<ColumnDescriptor>> ReadColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ColumnDescriptor>>([]);

    protected override Task<IReadOnlyList<StoredProcedureDescriptor>> ReadStoredProceduresAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredProcedureDescriptor>>([]);

    protected override Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ReadStoredProcedureParametersAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string procedure,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredProcedureParameterDescriptor>>([]);

    protected override Task<IReadOnlyList<EditableColumn>> ReadEditableColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EditableColumn>>([]);

    protected override ParameterizedStatement BuildTopRowsSelect(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        int maxRows) =>
        new(string.Empty, []);

    protected override ParameterizedStatement BuildCellUpdate(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableCellUpdate update) =>
        new(string.Empty, []);

    protected override ParameterizedStatement BuildRowInsert(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowInsert insert) =>
        new(string.Empty, []);

    protected override ParameterizedStatement BuildRowDelete(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowDelete delete) =>
        new(string.Empty, []);

    protected override Task<IReadOnlyList<DatabaseUserDescriptor>> ReadDatabaseUsersAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DatabaseUserDescriptor>>([]);

    protected override Task CreateUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task AlterUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDescriptor original,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task DropUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserName user,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<IReadOnlyList<ServerLoginDescriptor>> ReadServerLoginsAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ServerLoginDescriptor>>([]);

    protected override Task CreateLoginAsync(
        DbConnection connection,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task AlterLoginAsync(
        DbConnection connection,
        ServerLoginDescriptor original,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task DropLoginAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<IReadOnlyList<DatabaseRoleDescriptor>> ReadDatabaseRolesAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DatabaseRoleDescriptor>>([]);

    protected override Task CreateDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task AlterDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDescriptor original,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task DropDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        RoleName role,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<IReadOnlyList<ServerRoleDescriptor>> ReadServerRolesAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ServerRoleDescriptor>>([]);

    protected override Task CreateServerRoleAsync(
        DbConnection connection,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task AlterServerRoleAsync(
        DbConnection connection,
        ServerRoleDescriptor original,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task DropServerRoleAsync(
        DbConnection connection,
        RoleName role,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task CreateSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task AlterSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDescriptor original,
        SchemaDefinition definition,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task DropSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<IReadOnlyList<LoginUserMapping>> ReadLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LoginUserMapping>>([]);

    protected override Task WriteLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<IReadOnlyList<PermissionEntry>> ReadPermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PermissionEntry>>([]);

    protected override Task WritePermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<IReadOnlyList<SecurableReference>> ReadSecurablesAsync(
        DbConnection connection,
        SecurableKind kind,
        DatabaseName? database,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SecurableReference>>([]);
}
