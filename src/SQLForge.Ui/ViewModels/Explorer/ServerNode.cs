namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ツリーの根。接続 1 本を表し、その下にデータベースと、サーバー単位のセキュリティを持つ。
/// </summary>
public sealed class ServerNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;

    public ServerNode(CatalogContext context)
        : base(context.Session.Profile.Name, canExpand: true)
    {
        _context = context;
        Detail = context.Session.Server.Description;
    }

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken)
    {
        var children = new List<ObjectExplorerNode> { new CatalogFolderNode("データベース", LoadDatabasesAsync) };

        if (_context.ServerSecurity is not null)
        {
            children.Add(new CatalogFolderNode("セキュリティ", LoadSecurityAsync, showCount: false));
        }

        return Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(children);
    }

    /// <summary>
    /// SSMS と同じく、サーバーのセキュリティの下にログインの見出しを置く。
    /// サーバー ロールや資格情報はまだ扱わないので、今いるのはログインだけ。
    /// </summary>
    private Task<IReadOnlyList<ObjectExplorerNode>> LoadSecurityAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
            _context.ServerSecurity is { } security ? [new ServerLoginsNode(_context, security)] : []);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadDatabasesAsync(CancellationToken cancellationToken)
    {
        var databases = await _context.Databases
            .ExecuteAsync(_context.Session, cancellationToken)
            .ConfigureAwait(true);

        return databases.Select(database => new DatabaseNode(_context, database)).ToList();
    }
}
