using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Security;

/// <summary>
/// サーバー ロールの一覧。ログインのプロパティ ダイアログの
/// 「サーバー ロール」に並べる候補で、名前順に揃える。
/// </summary>
public sealed class ListServerRolesUseCase
{
    public async Task<IReadOnlyList<string>> ExecuteAsync(
        IDatabaseSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var roles = await session.ListServerRolesAsync(cancellationToken).ConfigureAwait(false);

        return roles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
