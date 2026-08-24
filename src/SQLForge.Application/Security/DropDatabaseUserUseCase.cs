using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ユーザーを 1 件削除する。取り消せない操作なので、
/// 触ってよい相手かどうかはサーバーへ送る前にここでも見る。
/// </summary>
public sealed class DropDatabaseUserUseCase
{
    public Task ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseUserDescriptor user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsSystem)
        {
            throw new DatabaseUserRejectedException("システムのユーザーは削除できません。");
        }

        return session.DropDatabaseUserAsync(database, user.Name, cancellationToken);
    }
}
