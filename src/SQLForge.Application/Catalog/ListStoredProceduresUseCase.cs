using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>スキーマ内のストアド プロシージャ一覧。名前順に並べる。</summary>
public sealed class ListStoredProceduresUseCase
{
    public async Task<IReadOnlyList<StoredProcedureDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var procedures = await session.ListStoredProceduresAsync(database, schema, cancellationToken)
            .ConfigureAwait(false);

        return CatalogOrdering.ByName(procedures).ToList();
    }
}
