namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「データベース」「スキーマ」「テーブル」の見出しノード。
/// 中身の取り方だけが違うので、取り方を渡してもらって共通で使う。
/// </summary>
/// <param name="showCount">
/// 読み終えた件数を見出しの右に出すか。中身が固定の見出し（「セキュリティ」）では、
/// 件数が「1」と出ても何も伝えないので消す。
/// </param>
public sealed class CatalogFolderNode(
    string title,
    Func<CancellationToken, Task<IReadOnlyList<ObjectExplorerNode>>> load,
    bool showCount = true) : ObjectExplorerNode(title, canExpand: true)
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<ObjectExplorerNode>>> _load = load;
    private readonly bool _showCount = showCount;

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        _load(cancellationToken);

    /// <summary>読み終えたら件数を見出しの右に出す（モックアップの「テーブル 48」）。</summary>
    protected override void OnChildrenLoaded(IReadOnlyList<ObjectExplorerNode> children) =>
        Detail = _showCount ? children.Count.ToString() : null;

    /// <summary>
    /// 失敗したら件数を消す。子が失敗の 1 行だけになっているのに前回の件数が残ると、
    /// 「3 件あるのに 1 行しか出ていない」という嘘の表示になる。
    /// </summary>
    protected override void OnChildrenFailed() => Detail = null;
}
