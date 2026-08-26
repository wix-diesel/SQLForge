using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// 主体 1 人に明示的に付いている権限。SSMS の「セキュリティ保護可能なリソース」ページに
/// 並べるもので、リソースの名前順・権限の名前順に揃える。
/// </summary>
public sealed class ListPermissionsUseCase
{
    public async Task<IReadOnlyList<PermissionEntry>> ExecuteAsync(
        IDatabaseSession session,
        SecurityPrincipal principal,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(principal);

        var permissions = await session.ListPermissionsAsync(principal, database, cancellationToken)
            .ConfigureAwait(false);

        return permissions
            .OrderBy(entry => entry.Securable.Kind)
            .ThenBy(entry => entry.Securable.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Permission, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
