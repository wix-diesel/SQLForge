using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>スキーマ内のテーブル一覧。名前順に並べる。</summary>
public sealed class ListTablesUseCase
{
    public async Task<IReadOnlyList<TableDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var tables = await session.ListTablesAsync(database, schema, cancellationToken).ConfigureAwait(false);

        return CatalogOrdering.ByName(tables).ToList();
    }
}
