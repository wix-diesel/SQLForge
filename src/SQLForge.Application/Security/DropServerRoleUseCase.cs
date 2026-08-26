using SQLForge.Application.Abstractions;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// サーバー ロールを 1 件削除する。取り消せない操作なので、
/// 触ってよい相手かどうかはサーバーへ送る前にここでも見る。
/// </summary>
public sealed class DropServerRoleUseCase
{
    public Task ExecuteAsync(
        IDatabaseSession session,
        ServerRoleDescriptor role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(role);

        if (role.IsFixedRole)
        {
            throw new ServerRoleRejectedException("固定のサーバー ロールは削除できません。");
        }

        return session.DropServerRoleAsync(role.Name, cancellationToken);
    }
}
