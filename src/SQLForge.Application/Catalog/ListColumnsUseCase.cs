using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>テーブルのカラム定義一覧。テーブル定義での並び順に揃える。</summary>
public sealed class ListColumnsUseCase
{
    public async Task<IReadOnlyList<ColumnDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var columns = await session.ListColumnsAsync(database, schema, table, cancellationToken).ConfigureAwait(false);

        return columns.OrderBy(column => column.OrdinalPosition).ToList();
    }
}
