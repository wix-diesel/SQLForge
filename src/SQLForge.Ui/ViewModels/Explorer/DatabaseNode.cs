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

    /// <summary>
    /// 開けないデータベースにはクエリも投げられない。作業領域がつながっていない構成
    /// （ツリーだけを組むとき）も同じで、押せるのに何も起きないメニューは出さない。
    /// </summary>
    public bool CanQuery => _descriptor.IsAccessible && _context.Query is not null;

    /// <summary>右クリックの「新しいクエリ」。このデータベースを実行先にして空のエディタを開く。</summary>
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void OpenQuery() => _context.Query?.OpenNewQuery(_descriptor.Name);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken)
    {
        var children = new List<ObjectExplorerNode> { new CatalogFolderNode("スキーマ", LoadSchemasAsync) };

        if (_context.Security is not null)
        {
            children.Add(new CatalogFolderNode("セキュリティ", LoadSecurityAsync, showCount: false));
        }

        return Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(children);
    }

    /// <summary>
    /// SSMS と同じく、セキュリティの下にユーザーの見出しを置く。
    /// ロールやスキーマの所有権はまだ扱わないので、今いるのはユーザーだけ。
    /// </summary>
    private Task<IReadOnlyList<ObjectExplorerNode>> LoadSecurityAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
            _context.Security is { } security
                ? [new DatabaseUsersNode(_context, security, _descriptor.Name)]
                : []);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadSchemasAsync(CancellationToken cancellationToken)
    {
        var schemas = await _context.Schemas
            .ExecuteAsync(_context.Session, _descriptor.Name, cancellationToken)
            .ConfigureAwait(true);

        return schemas.Select(schema => new SchemaNode(_context, _descriptor.Name, schema)).ToList();
    }
}
