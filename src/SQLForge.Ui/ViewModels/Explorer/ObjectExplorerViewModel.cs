using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// メインウィンドウ左のオブジェクトエクスプローラー。
/// 根（接続）だけを持ち、その下は展開に合わせて読む。
/// </summary>
public sealed partial class ObjectExplorerViewModel : ObservableObject
{
    private readonly CatalogContext _context;

    [ObservableProperty] private ObjectExplorerNode? _selectedNode;

    public ObjectExplorerViewModel(CatalogContext context)
    {
        _context = context;
        Roots = [];
        Reset();
    }

    public ObservableCollection<ObjectExplorerNode> Roots { get; }

    /// <summary>
    /// 起動直後に開いておく段。接続してすぐデータベースの一覧が見える状態にする。
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var server = Roots[0];
        server.IsExpanded = true;
        await server.EnsureChildrenAsync(cancellationToken).ConfigureAwait(true);

        if (server.Children.FirstOrDefault() is { CanExpand: true } databases)
        {
            databases.IsExpanded = true;
            await databases.EnsureChildrenAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>ツリーを作り直して読み直す。展開状態も選択も初期状態へ戻る。</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Reset();
        await InitializeAsync(cancellationToken).ConfigureAwait(true);
    }

    private void Reset()
    {
        SelectedNode = null;
        Roots.Clear();
        Roots.Add(new ServerNode(_context));
    }
}
