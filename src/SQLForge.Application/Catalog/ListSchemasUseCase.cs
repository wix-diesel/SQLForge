using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>データベース内のスキーマ一覧。システムスキーマは後ろへ回す。</summary>
public sealed class ListSchemasUseCase
{
    public async Task<IReadOnlyList<SchemaDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var schemas = await session.ListSchemasAsync(database, cancellationToken).ConfigureAwait(false);

        return CatalogOrdering
            .UserObjectsFirst(schemas, schema => schema.IsSystem, schema => schema.Name.Value)
            .ToList();
    }
}
