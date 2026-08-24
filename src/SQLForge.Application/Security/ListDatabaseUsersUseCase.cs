using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// データベース内のユーザー一覧。カタログと同じく、利用者が作ったものを先に、
/// エンジンが用意したもの（dbo・guest・sys）を後ろへ回す。
/// </summary>
public sealed class ListDatabaseUsersUseCase
{
    public async Task<IReadOnlyList<DatabaseUserDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var users = await session.ListDatabaseUsersAsync(database, cancellationToken).ConfigureAwait(false);

        return CatalogOrdering
            .UserObjectsFirst(users, user => user.IsSystem, user => user.Name.Value)
            .ToList();
    }
}
