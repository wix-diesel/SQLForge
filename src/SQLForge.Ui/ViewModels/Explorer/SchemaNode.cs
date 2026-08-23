using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>スキーマ 1 件。下にテーブルの見出しを持つ。</summary>
public sealed class SchemaNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseName _database;
    private readonly SchemaDescriptor _descriptor;

    public SchemaNode(CatalogContext context, DatabaseName database, SchemaDescriptor descriptor)
        : base(descriptor.Name.Value, canExpand: true, isSystem: descriptor.IsSystem)
    {
        _context = context;
        _database = database;
        _descriptor = descriptor;
    }

    public SchemaName Name => _descriptor.Name;

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
        [
            new CatalogFolderNode("テーブル", LoadTablesAsync)
        ]);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadTablesAsync(CancellationToken cancellationToken)
    {
        var tables = await _context.Tables
            .ExecuteAsync(_context.Session, _database, _descriptor.Name, cancellationToken)
            .ConfigureAwait(true);

        return tables.Select(table => new TableNode(_context, _database, table)).ToList();
    }
}
