namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>ツリーの根。接続 1 本を表し、その下にデータベースの見出しを持つ。</summary>
public sealed class ServerNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;

    public ServerNode(CatalogContext context)
        : base(context.Session.Profile.Name, canExpand: true)
    {
        _context = context;
        Detail = context.Session.Server.Description;
    }

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
        [
            new CatalogFolderNode("データベース", LoadDatabasesAsync)
        ]);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadDatabasesAsync(CancellationToken cancellationToken)
    {
        var databases = await _context.Databases
            .ExecuteAsync(_context.Session, cancellationToken)
            .ConfigureAwait(true);

        return databases.Select(database => new DatabaseNode(_context, database)).ToList();
    }
}
