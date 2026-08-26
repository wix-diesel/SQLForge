using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// データベース ロールの一覧。ツリーの「データベース ロール」の見出しと、
/// ユーザーのプロパティ ダイアログの「メンバーシップ」に並べる候補を兼ねる。
///
/// SSMS と同じく、固定ロール（db_owner など）も混ぜて名前順に揃える。
/// </summary>
public sealed class ListDatabaseRolesUseCase
{
    public async Task<IReadOnlyList<DatabaseRoleDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var roles = await session.ListDatabaseRolesAsync(database, cancellationToken).ConfigureAwait(false);

        return roles.OrderBy(role => role.Name.Value, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
