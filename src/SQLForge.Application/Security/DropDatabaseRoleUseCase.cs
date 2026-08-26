using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// データベース ロールを 1 件削除する。取り消せない操作なので、
/// 触ってよい相手かどうかはサーバーへ送る前にここでも見る。
/// </summary>
public sealed class DropDatabaseRoleUseCase
{
    public Task ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseRoleDescriptor role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(role);

        if (role.IsFixedRole)
        {
            throw new DatabaseRoleRejectedException("固定のデータベース ロールは削除できません。");
        }

        return session.DropDatabaseRoleAsync(database, role.Name, cancellationToken);
    }
}
