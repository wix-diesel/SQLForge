using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// サーバー上のログイン一覧。カタログと同じく、利用者が作ったものを先に、
/// エンジンが用意したもの（sa や ## で始まるもの）を後ろへ回す。
/// </summary>
public sealed class ListServerLoginsUseCase
{
    public async Task<IReadOnlyList<ServerLoginDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var logins = await session.ListServerLoginsAsync(cancellationToken).ConfigureAwait(false);

        return CatalogOrdering
            .UserObjectsFirst(logins, login => login.IsSystem, login => login.Name.Value)
            .ToList();
    }
}
