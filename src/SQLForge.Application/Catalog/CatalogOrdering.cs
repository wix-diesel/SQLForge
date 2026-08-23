using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>
/// カタログの並べ方。どのエンジンでも「利用者が作ったものが先、エンジンが用意したものが後、
/// その中では名前順」に揃える。エンジンごとの ORDER BY 任せにしないのは、
/// 照合順序で並びが変わってしまうため。
/// </summary>
internal static class CatalogOrdering
{
    public static IOrderedEnumerable<T> UserObjectsFirst<T>(
        IEnumerable<T> items,
        Func<T, bool> isSystem,
        Func<T, string> name) =>
        items
            .OrderBy(item => isSystem(item) ? 1 : 0)
            .ThenBy(name, StringComparer.OrdinalIgnoreCase);

    public static IOrderedEnumerable<TableDescriptor> ByName(IEnumerable<TableDescriptor> tables) =>
        tables.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase);

    public static IOrderedEnumerable<StoredProcedureDescriptor> ByName(
        IEnumerable<StoredProcedureDescriptor> procedures) =>
        procedures.OrderBy(procedure => procedure.Name, StringComparer.OrdinalIgnoreCase);
}
