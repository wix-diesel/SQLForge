using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Security;

/// <summary>
/// データベース ロールの一覧。ユーザーのプロパティ ダイアログの
/// 「メンバーシップ」に並べる候補で、名前順に揃える。
/// </summary>
public sealed class ListDatabaseRolesUseCase
{
    public async Task<IReadOnlyList<string>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var roles = await session.ListDatabaseRolesAsync(database, cancellationToken).ConfigureAwait(false);

        return roles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
