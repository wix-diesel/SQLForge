namespace SQLForge.Ui.ViewModels;

/// <summary>接続ダイアログのタブ 1 枚。「一般」以外はまだ中身がない。</summary>
public sealed class DialogTabViewModel(string title, bool isImplemented, string placeholder = "")
{
    public string Title { get; } = title;

    public bool IsImplemented { get; } = isImplemented;

    /// <summary>未実装タブに出す説明。</summary>
    public string Placeholder { get; } = placeholder;
}
