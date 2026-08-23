using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>データベース 1 件。下にスキーマの見出しを持つ。</summary>
public sealed partial class DatabaseNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseDescriptor _descriptor;

    public DatabaseNode(CatalogContext context, DatabaseDescriptor descriptor)
        // 開けないデータベース（オフライン・権限なし）は展開させない。
        : base(descriptor.Name.Value, canExpand: descriptor.IsAccessible, isSystem: descriptor.IsSystem)
    {
        _context = context;
        _descriptor = descriptor;

        if (!descriptor.IsAccessible)
        {
            Detail = "アクセスできません";
        }
    }

    public DatabaseName Name => _descriptor.Name;

    public string? Collation => _descriptor.Collation;

    /// <summary>開けないデータベースにはクエリも投げられない。</summary>
    public bool CanQuery => _descriptor.IsAccessible;

    /// <summary>右クリックの「新しいクエリ」。このデータベースを実行先にして空のエディタを開く。</summary>
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void OpenQuery() => _context.Query?.OpenNewQuery(_descriptor.Name);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
        [
            new CatalogFolderNode("スキーマ", LoadSchemasAsync)
        ]);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadSchemasAsync(CancellationToken cancellationToken)
    {
        var schemas = await _context.Schemas
            .ExecuteAsync(_context.Session, _descriptor.Name, cancellationToken)
            .ConfigureAwait(true);

        return schemas.Select(schema => new SchemaNode(_context, _descriptor.Name, schema)).ToList();
    }
}
