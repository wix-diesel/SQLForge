using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// セキュリティ（ロール）の受け持ち。データベース ロールはユーザーと同じくデータベースの中で、
/// サーバー ロールはログインと同じくサーバー スコープで扱う。
///
/// どちらも「作る・所有者を移す・メンバーを出し入れする」が複数の文に分かれるので、
/// 文面の並びはドライバーが決め、ここは流す場所を決めるだけにする。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task CreateDatabaseRoleAsync(
        DatabaseName database,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => CreateDatabaseRoleAsync(connection, database, definition, token),
            cancellationToken);
    }

    public Task AlterDatabaseRoleAsync(
        DatabaseName database,
        DatabaseRoleDescriptor original,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => AlterDatabaseRoleAsync(connection, database, original, definition, token),
            cancellationToken);
    }

    public Task DropDatabaseRoleAsync(
        DatabaseName database,
        RoleName role,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            (connection, token) => DropDatabaseRoleAsync(connection, database, role, token),
            cancellationToken);

    public Task CreateServerRoleAsync(
        ServerRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => CreateServerRoleAsync(connection, definition, token),
            cancellationToken);
    }

    public Task AlterServerRoleAsync(
        ServerRoleDescriptor original,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => AlterServerRoleAsync(connection, original, definition, token),
            cancellationToken);
    }

    public Task DropServerRoleAsync(RoleName role, CancellationToken cancellationToken = default) =>
        WriteAsync((connection, token) => DropServerRoleAsync(connection, role, token), cancellationToken);

    protected abstract Task CreateDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task AlterDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDescriptor original,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task DropDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        RoleName role,
        CancellationToken cancellationToken);

    protected abstract Task CreateServerRoleAsync(
        DbConnection connection,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task AlterServerRoleAsync(
        DbConnection connection,
        ServerRoleDescriptor original,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task DropServerRoleAsync(
        DbConnection connection,
        RoleName role,
        CancellationToken cancellationToken);
}
