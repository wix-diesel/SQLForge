using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// オブジェクトエクスプローラーのツリー 1 ノード。
///
/// 子は展開されたときに初めて読む。ツリーは展開されるまで子の数を知らないので、
/// 展開できるノードには最初から「読み込み中」の 1 件を入れておき、
/// 読み終えたら差し替える（そうしないと展開の矢印が出ない）。
/// </summary>
public abstract partial class ObjectExplorerNode : ObservableObject
{
    private bool _childrenLoaded;

    protected ObjectExplorerNode(string title, bool canExpand, bool isSystem = false)
    {
        Title = title;
        CanExpand = canExpand;
        IsSystem = isSystem;

        if (canExpand)
        {
            Children.Add(MessageNode.Loading());
        }
    }

    public string Title { get; }

    /// <summary>子を持ちうるか。テーブルのような葉は false。</summary>
    public bool CanExpand { get; }

    /// <summary>エンジンが用意したもの（master、sys など）。表示を控えめにする。</summary>
    public bool IsSystem { get; }

    public ObservableCollection<ObjectExplorerNode> Children { get; } = [];

    /// <summary>行の右側に出す補足（サーバーのバージョン、件数、行数）。</summary>
    [ObservableProperty] private string? _detail;

    [ObservableProperty] private bool _isExpanded;

    [ObservableProperty] private bool _isLoading;

    /// <summary>読み込みに失敗した理由。ノード自身の行ではなく子として出す。</summary>
    [ObservableProperty] private string? _error;

    /// <summary>子を読む。すでに読んであれば何もしない。</summary>
    public async Task EnsureChildrenAsync(CancellationToken cancellationToken = default)
    {
        // 読み込み中に開き直されても二重に取りにいかない（展開は何度でも起きうる）。
        if (_childrenLoaded || IsLoading || !CanExpand)
        {
            return;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>子を読み直す。失敗しても投げず、理由を子の行として見せる。</summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanExpand)
        {
            return;
        }

        IsLoading = true;
        Error = null;

        try
        {
            var children = await LoadChildrenAsync(cancellationToken).ConfigureAwait(true);
            Replace(children.Count == 0 ? [MessageNode.Empty()] : children);
            _childrenLoaded = true;
            OnChildrenLoaded(children);
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた・別の読み込みに追い越された。何もしない。
        }
        catch (Exception exception)
        {
            // 権限不足やオフラインのデータベースはここへ来る。次の展開でやり直せるよう、読み込み済みにはしない。
            Error = exception.Message;
            Replace([MessageNode.Failed(exception.Message)]);
            _childrenLoaded = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>子を実際に取ってくる。派生クラスが埋める。</summary>
    protected abstract Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken);

    /// <summary>読み終えた直後に呼ばれる。件数の表示などに使う。</summary>
    protected virtual void OnChildrenLoaded(IReadOnlyList<ObjectExplorerNode> children)
    {
    }

    // 展開は UI スレッドの操作なので、読み込みは投げっぱなしにする（ReloadAsync が例外を受け止める）。
    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
        {
            _ = EnsureChildrenAsync();
        }
    }

    private void Replace(IReadOnlyList<ObjectExplorerNode> children)
    {
        Children.Clear();

        foreach (var child in children)
        {
            Children.Add(child);
        }
    }
}
