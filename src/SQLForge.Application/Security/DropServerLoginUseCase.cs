using SQLForge.Application.Abstractions;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ログインを 1 件削除する。取り消せない操作なので、
/// 触ってよい相手かどうかはサーバーへ送る前にここでも見る。
/// </summary>
public sealed class DropServerLoginUseCase
{
    public Task ExecuteAsync(
        IDatabaseSession session,
        ServerLoginDescriptor login,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(login);

        if (login.IsSystem)
        {
            throw new ServerLoginRejectedException("システムのログインは削除できません。");
        }

        return session.DropServerLoginAsync(login.Name, cancellationToken);
    }
}
