using SQLForge.Application.Abstractions;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// サーバー ロールの一覧。ツリーの「サーバー ロール」の見出しと、
/// ログインのプロパティ ダイアログの「サーバー ロール」に並べる候補を兼ねる。
///
/// SSMS と同じく、固定ロール（sysadmin など）も混ぜて名前順に揃える。
/// </summary>
public sealed class ListServerRolesUseCase
{
    public async Task<IReadOnlyList<ServerRoleDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var roles = await session.ListServerRolesAsync(cancellationToken).ConfigureAwait(false);

        return roles.OrderBy(role => role.Name.Value, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
