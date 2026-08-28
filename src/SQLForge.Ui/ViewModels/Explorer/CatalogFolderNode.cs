namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「データベース」「テーブル」「列」のような見出しノード。
/// 中身の取り方だけが違うので、取り方を渡してもらって共通で使う。
/// 件数の表示と絞り込みは <see cref="FolderNode"/> が受け持つ。
/// </summary>
/// <param name="showCount">
/// 読み終えた件数を見出しの右に出すか。中身が固定の見出し（「セキュリティ」）では、
/// 件数が「1」と出ても何も伝えないので消す。
/// </param>
/// <param name="filter">
/// 絞り込みの支度。渡さない見出し（「列」「パラメーター」など、SSMS でも絞り込めないもの）には
/// フィルターのメニューを出さない。
/// </param>
public sealed class CatalogFolderNode(
    string title,
    Func<CancellationToken, Task<IReadOnlyList<ObjectExplorerNode>>> load,
    bool showCount = true,
    ObjectFilterSpec? filter = null) : FolderNode(title, filter, showCount)
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<ObjectExplorerNode>>> _load = load;

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        _load(cancellationToken);
}
