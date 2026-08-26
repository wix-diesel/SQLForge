using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// セキュリティ保護可能なリソースと権限の受け持ち。
///
/// 主体がサーバー スコープ（ログイン・サーバー ロール）ならサーバーの権限を、
/// データベース スコープ（ユーザー・データベース ロール）ならそのデータベースの中の権限を
/// 読み書きする。どちらになるかは主体そのものが知っているので、ここでは振り分けない。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task<IReadOnlyList<PermissionEntry>> ListPermissionsAsync(
        SecurityPrincipal principal,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return QueryAsync(
            (connection, token) => ReadPermissionsAsync(connection, principal, database, token),
            cancellationToken);
    }

    public Task ApplyPermissionsAsync(
        SecurityPrincipal principal,
        DatabaseName? database,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(desired);

        return WriteAsync(
            (connection, token) => WritePermissionsAsync(connection, principal, database, original, desired, token),
            cancellationToken);
    }

    public Task<IReadOnlyList<SecurableReference>> ListSecurablesAsync(
        SecurableKind kind,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default) =>
        QueryAsync((connection, token) => ReadSecurablesAsync(connection, kind, database, token), cancellationToken);

    protected abstract Task<IReadOnlyList<PermissionEntry>> ReadPermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        CancellationToken cancellationToken);

    protected abstract Task WritePermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<SecurableReference>> ReadSecurablesAsync(
        DbConnection connection,
        SecurableKind kind,
        DatabaseName? database,
        CancellationToken cancellationToken);
}
