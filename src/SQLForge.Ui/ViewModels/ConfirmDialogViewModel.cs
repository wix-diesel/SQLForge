using CommunityToolkit.Mvvm.Input;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 「本当に削除しますか」のような一言だけのダイアログ。
/// 取り消せない操作の前と、操作が失敗したことを伝えるのに使う。
/// </summary>
public sealed partial class ConfirmDialogViewModel
{
    private ConfirmDialogViewModel(
        string title,
        string headline,
        string detail,
        string confirmLabel,
        bool isDestructive,
        bool canCancel)
    {
        Title = title;
        Headline = headline;
        Detail = detail;
        ConfirmLabel = confirmLabel;
        IsDestructive = isDestructive;
        CanCancel = canCancel;
    }

    public string Title { get; }

    public string Headline { get; }

    public string Detail { get; }

    public string ConfirmLabel { get; }

    /// <summary>取り消せない操作か。確定ボタンを警告色にする。</summary>
    public bool IsDestructive { get; }

    /// <summary>やめられるか。知らせるだけのときは閉じるしかない。</summary>
    public bool CanCancel { get; }

    /// <summary>閉じることを伝える。true なら確定が押された。</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>取り消せない操作の確認。</summary>
    public static ConfirmDialogViewModel Destructive(string title, string headline, string detail, string confirmLabel) =>
        new(title, headline, detail, confirmLabel, isDestructive: true, canCancel: true);

    /// <summary>失敗を知らせるだけ。</summary>
    public static ConfirmDialogViewModel Alert(string title, string headline, string detail) =>
        new(title, headline, detail, "閉じる", isDestructive: false, canCancel: false);

    [RelayCommand]
    private void Confirm() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
