namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 中身の代わりに一言だけ出す行。読み込み中・空・失敗の 3 通りに使う。
/// ツリーの中で完結させるため、別のパネルには出さない。
/// </summary>
public sealed class MessageNode : ObjectExplorerNode
{
    private MessageNode(string title, bool isFailure)
        : base(title, canExpand: false) => IsFailure = isFailure;

    /// <summary>失敗の行は赤で出す。</summary>
    public bool IsFailure { get; }

    public static MessageNode Loading() => new("読み込み中…", isFailure: false);

    public static MessageNode Empty() => new("（なし）", isFailure: false);

    public static MessageNode Failed(string reason) => new(reason, isFailure: true);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);
}
