namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「データベース」「スキーマ」「テーブル」の見出しノード。
/// 中身の取り方だけが違うので、取り方を渡してもらって共通で使う。
/// </summary>
public sealed class CatalogFolderNode(
    string title,
    Func<CancellationToken, Task<IReadOnlyList<ObjectExplorerNode>>> load) : ObjectExplorerNode(title, canExpand: true)
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<ObjectExplorerNode>>> _load = load;

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        _load(cancellationToken);

    /// <summary>読み終えたら件数を見出しの右に出す（モックアップの「テーブル 48」）。</summary>
    protected override void OnChildrenLoaded(IReadOnlyList<ObjectExplorerNode> children) =>
        Detail = children.Count.ToString();
}
