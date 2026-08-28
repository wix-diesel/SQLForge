using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;
using SQLForge.Domain.Security;
using SQLForge.Infrastructure.Connections;

namespace SQLForge.Infrastructure.PostgreSql;

/// <summary>
/// この版の PostgreSQL ドライバーがまだ受け持たない操作。
///
/// 口の形は DBMS を問わず 1 つなので、実装の進み具合に関わらず全部を埋める必要がある。
/// できないことは <see cref="Domain.Connections.SessionCapabilities.CatalogOnly"/> として先に申告してあり、
/// 画面はそれを見てメニューも枝も出さないので、ここへ来るのは
/// 画面を通らずに呼ばれたときだけになる。そのときに黙って空を返すと
/// 「権限が無くて 0 件」と見分けが付かないので、理由を付けて断る。
/// </summary>
public sealed partial class PostgreSqlSession
{
    private static NotSupportedException NotYet(string operation) =>
        new($"PostgreSQL ドライバーは{operation}にまだ対応していません。");

    protected override Task<IReadOnlyList<EditableColumn>> ReadEditableColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken) =>
        throw NotYet("テーブルの編集");

    protected override ParameterizedStatement BuildTopRowsSelect(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        int maxRows) =>
        throw NotYet("テーブルの編集");

    protected override ParameterizedStatement BuildCellUpdate(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableCellUpdate update) =>
        throw NotYet("テーブルの編集");

    protected override ParameterizedStatement BuildRowInsert(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowInsert insert) =>
        throw NotYet("テーブルの編集");

    protected override ParameterizedStatement BuildRowDelete(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowDelete delete) =>
        throw NotYet("テーブルの編集");

    protected override Task<IReadOnlyList<DatabaseUserDescriptor>> ReadDatabaseUsersAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ユーザーの読み取り");

    protected override Task CreateUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ユーザーの作成");

    protected override Task AlterUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDescriptor original,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ユーザーの変更");

    protected override Task DropUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserName user,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ユーザーの削除");

    protected override Task<IReadOnlyList<ServerLoginDescriptor>> ReadServerLoginsAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        throw NotYet("ログインの読み取り");

    protected override Task CreateLoginAsync(
        DbConnection connection,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("ログインの作成");

    protected override Task AlterLoginAsync(
        DbConnection connection,
        ServerLoginDescriptor original,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("ログインの変更");

    protected override Task DropLoginAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken) =>
        throw NotYet("ログインの削除");

    protected override Task<IReadOnlyList<DatabaseRoleDescriptor>> ReadDatabaseRolesAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ロールの読み取り");

    protected override Task CreateDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ロールの作成");

    protected override Task AlterDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDescriptor original,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ロールの変更");

    protected override Task DropDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        RoleName role,
        CancellationToken cancellationToken) =>
        throw NotYet("データベース ロールの削除");

    protected override Task<IReadOnlyList<ServerRoleDescriptor>> ReadServerRolesAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        throw NotYet("サーバー ロールの読み取り");

    protected override Task CreateServerRoleAsync(
        DbConnection connection,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("サーバー ロールの作成");

    protected override Task AlterServerRoleAsync(
        DbConnection connection,
        ServerRoleDescriptor original,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("サーバー ロールの変更");

    protected override Task DropServerRoleAsync(
        DbConnection connection,
        RoleName role,
        CancellationToken cancellationToken) =>
        throw NotYet("サーバー ロールの削除");

    protected override Task CreateSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("スキーマの作成");

    protected override Task AlterSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaDescriptor original,
        SchemaDefinition definition,
        CancellationToken cancellationToken) =>
        throw NotYet("スキーマの変更");

    protected override Task DropSchemaAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken) =>
        throw NotYet("スキーマの削除");

    protected override Task<IReadOnlyList<LoginUserMapping>> ReadLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken) =>
        throw NotYet("ユーザー マッピングの読み取り");

    protected override Task WriteLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired,
        CancellationToken cancellationToken) =>
        throw NotYet("ユーザー マッピングの変更");

    protected override Task<IReadOnlyList<PermissionEntry>> ReadPermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        CancellationToken cancellationToken) =>
        throw NotYet("権限の読み取り");

    protected override Task WritePermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired,
        CancellationToken cancellationToken) =>
        throw NotYet("権限の変更");

    protected override Task<IReadOnlyList<SecurableReference>> ReadSecurablesAsync(
        DbConnection connection,
        SecurableKind kind,
        DatabaseName? database,
        CancellationToken cancellationToken) =>
        throw NotYet("権限を付けられるリソースの読み取り");
}
