using CommunityToolkit.Mvvm.Input;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ツリーの根。接続 1 本を表し、その下にデータベースと、サーバー単位のセキュリティを持つ。
/// </summary>
public sealed partial class ServerNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;

    public ServerNode(CatalogContext context)
        : base(context.Session.Profile.Name, canExpand: true)
    {
        _context = context;
        Detail = context.Session.Server.Description;
    }

    /// <summary>右クリックの「接続解除」。起動時の画面に戻る。</summary>
    [RelayCommand]
    private void Disconnect() => _context.ConnectionLauncher?.Disconnect();

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
    /// SSMS と同じく、サーバーのセキュリティの下にログインとサーバー ロールの見出しを置く。
    /// 資格情報や監査はまだ扱わない。
    /// </summary>
    private Task<IReadOnlyList<ObjectExplorerNode>> LoadSecurityAsync(CancellationToken cancellationToken)
    {
        if (_context.ServerSecurity is not { } security)
        {
            return Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);
        }

        var children = new List<ObjectExplorerNode> { new ServerLoginsNode(_context, security) };

        if (security.Roles is not null)
        {
            children.Add(new ServerRolesNode(_context, security));
        }

        return Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(children);
    }

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadDatabasesAsync(CancellationToken cancellationToken)
    {
        var databases = await _context.Databases
            .ExecuteAsync(_context.Session, cancellationToken)
            .ConfigureAwait(true);

        return databases.Select(database => new DatabaseNode(_context, database)).ToList();
    }
}
