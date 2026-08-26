using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.Tests;

/// <summary>
/// セキュリティのダイアログを組み立てる。ユースケースはどれも状態を持たないので、
/// テストのたびに新しく作って渡す。ページ（権限・ユーザー マッピング）まで含めて
/// 本物と同じ形に組むのは、ダイアログ サービスと同じ手順をここでもなぞるため。
/// </summary>
internal static class SecurityDialogs
{
    public static DatabaseUserDialogViewModel User(
        FakeDatabaseSession session,
        DatabaseName database,
        DatabaseUserDescriptor? user = null) =>
        new(
            session,
            database,
            user,
            new ListSchemasUseCase(),
            new ListDatabaseRolesUseCase(),
            new SaveDatabaseUserUseCase(),
            new SavePermissionsUseCase(),
            Securables(session, SecurityPrincipalKind.DatabaseUser, user?.Name.Value, database));

    public static ServerLoginDialogViewModel Login(
        FakeDatabaseSession session,
        ServerLoginDescriptor? login = null) =>
        new(
            session,
            login,
            new ListDatabasesUseCase(),
            new ListServerRolesUseCase(),
            new SaveServerLoginUseCase(),
            new SavePermissionsUseCase(),
            new LoginUserMappingsViewModel(
                session,
                login?.Name.Value ?? string.Empty,
                isNewLogin: login is null,
                new ListDatabasesUseCase(),
                new ListDatabaseRolesUseCase(),
                new ListLoginUserMappingsUseCase()),
            Securables(session, SecurityPrincipalKind.ServerLogin, login?.Name.Value, database: null));

    public static DatabaseRoleDialogViewModel DatabaseRole(
        FakeDatabaseSession session,
        DatabaseName database,
        DatabaseRoleDescriptor? role = null) =>
        new(
            session,
            database,
            role,
            new ListDatabaseUsersUseCase(),
            new ListDatabaseRolesUseCase(),
            new ListSchemasUseCase(),
            new SaveDatabaseRoleUseCase(),
            new SavePermissionsUseCase(),
            Securables(session, SecurityPrincipalKind.DatabaseRole, role?.Name.Value, database));

    public static ServerRoleDialogViewModel ServerRole(
        FakeDatabaseSession session,
        ServerRoleDescriptor? role = null) =>
        new(
            session,
            role,
            new ListServerLoginsUseCase(),
            new ListServerRolesUseCase(),
            new SaveServerRoleUseCase(),
            new SavePermissionsUseCase(),
            Securables(session, SecurityPrincipalKind.ServerRole, role?.Name.Value, database: null));

    public static SchemaDialogViewModel Schema(
        FakeDatabaseSession session,
        DatabaseName database,
        SchemaDescriptor? schema = null) =>
        new(
            session,
            database,
            schema,
            new ListDatabaseUsersUseCase(),
            new ListDatabaseRolesUseCase(),
            new SaveSchemaUseCase());

    private static SecurablePermissionsViewModel Securables(
        FakeDatabaseSession session,
        SecurityPrincipalKind kind,
        string? name,
        DatabaseName? database) =>
        new(session, kind, name, database, new ListPermissionsUseCase(), new ListSecurablesUseCase());
}
