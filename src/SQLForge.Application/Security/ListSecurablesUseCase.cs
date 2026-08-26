using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// グリッドへ足せるリソースの候補。SSMS の「セキュリティ保護可能なリソースの追加」で
/// 種類を選んだあとに出る一覧にあたり、名前順に揃える。
/// </summary>
public sealed class ListSecurablesUseCase
{
    public async Task<IReadOnlyList<SecurableReference>> ExecuteAsync(
        IDatabaseSession session,
        SecurableKind kind,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var securables = await session.ListSecurablesAsync(kind, database, cancellationToken).ConfigureAwait(false);

        return securables
            .OrderBy(securable => securable.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
