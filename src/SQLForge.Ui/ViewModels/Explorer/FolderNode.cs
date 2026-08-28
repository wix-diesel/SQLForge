using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Filtering;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 中身の一覧を持つ見出しノード（「データベース」「テーブル」「ユーザー」など）の共通部分。
///
/// 読み終えた件数を右に出すことと、SSMS と同じ絞り込み（フィルター）はどの見出しでも同じなので、
/// ここでまとめて受け持つ。絞り込みの条件は見出しごとに持ち、掛かっている間は
/// SSMS と同じく見出しに「(フィルター適用)」を添える。
/// </summary>
public abstract partial class FolderNode : ObjectExplorerNode
{
    private readonly ObjectFilterSpec? _filter;
    private readonly bool _showCount;

    /// <param name="filter">絞り込みの支度。無ければ右クリックにフィルターのメニューを出さない。</param>
    /// <param name="showCount">
    /// 読み終えた件数を見出しの右に出すか。中身が固定の見出し（「セキュリティ」）では、
    /// 件数が「1」と出ても何も伝えないので消す。
    /// </param>
    protected FolderNode(string title, ObjectFilterSpec? filter = null, bool showCount = true)
        : base(title, canExpand: true)
    {
        _filter = filter;
        _showCount = showCount;
    }

    /// <summary>今かかっている絞り込み。掛かっていなければ <see cref="ObjectFilter.None"/>。</summary>
    public ObjectFilter Filter { get; private set; } = ObjectFilter.None;

    public bool IsFiltered => !Filter.IsEmpty;

    /// <summary>
    /// ツリーに出す見出し。SSMS と同じく、絞り込みが掛かっている間は「(フィルター適用)」を添えて、
    /// 一覧が全部ではないことを行そのもので分かるようにする。
    /// </summary>
    public string DisplayTitle => IsFiltered ? $"{Title} (フィルター適用)" : Title;

    /// <summary>絞り込みのメニューを出してよいか。行き先が無い構成では押せるだけのメニューを出さない。</summary>
    public bool CanFilter => _filter is { Editor: not null } spec && spec.Properties.Count > 0;

    /// <summary>「フィルターの削除」を押せるか。掛かっていないときは押せない（SSMS と同じ）。</summary>
    public bool CanRemoveFilter => CanFilter && IsFiltered;

    /// <summary>右クリックの「フィルター」→「フィルターの設定…」。</summary>
    [RelayCommand(CanExecute = nameof(CanFilter))]
    private async Task EditFilterAsync(CancellationToken cancellationToken)
    {
        if (_filter is not { Editor: { } editor } spec)
        {
            return;
        }

        var edited = await editor
            .EditAsync(spec.Describe(Title), spec.Properties, Filter)
            .ConfigureAwait(true);

        // キャンセルなら今の条件のまま。読み直しもしない。
        if (edited is null)
        {
            return;
        }

        await ApplyFilterAsync(edited, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>右クリックの「フィルター」→「フィルターの削除」。条件を捨てて一覧を読み直す。</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveFilter))]
    private Task RemoveFilterAsync(CancellationToken cancellationToken) =>
        ApplyFilterAsync(ObjectFilter.None, cancellationToken);

    /// <summary>右クリックの「最新の情報に更新」。絞り込みは掛けたまま読み直す。</summary>
    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken) => ReloadAsync(cancellationToken);

    /// <summary>読み終えたら件数を見出しの右に出す（モックアップの「テーブル 48」）。</summary>
    protected override void OnChildrenLoaded(IReadOnlyList<ObjectExplorerNode> children) =>
        Detail = _showCount ? children.Count.ToString() : null;

    /// <summary>
    /// 失敗したら件数を消す。子が失敗の 1 行だけになっているのに前回の件数が残ると、
    /// 「3 件あるのに 1 行しか出ていない」という嘘の表示になる。
    /// </summary>
    protected override void OnChildrenFailed() => Detail = null;

    /// <summary>読んだ一覧から、条件に当てはまるものだけを残す。</summary>
    protected override IReadOnlyList<ObjectExplorerNode> FilterChildren(IReadOnlyList<ObjectExplorerNode> children) =>
        Filter.IsEmpty
            ? children
            : children.Where(child => Filter.Matches(child.FilterTarget)).ToList();

    private async Task ApplyFilterAsync(ObjectFilter filter, CancellationToken cancellationToken)
    {
        Filter = filter;

        OnPropertyChanged(nameof(Filter));
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(CanRemoveFilter));
        RemoveFilterCommand.NotifyCanExecuteChanged();

        // SSMS と同じく、条件を変えたらサーバーから読み直す（この間に増えたものも条件で見る）。
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }
}
