using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 編集グリッドのセル 1 つ。
///
/// SSMS の編集グリッドと同じ手順にする。セルを開く（<see cref="BeginEdit"/>）→ 打ち直す →
/// 確定（<see cref="CommitAsync"/>）でその 1 セルだけを書き戻し、Esc（<see cref="CancelEdit"/>）で
/// 元へ戻す。確定してもサーバーが受け付けるまでは表示を変えない（失敗したときに、
/// 画面だけが新しい値になっているのを避けるため）。
/// </summary>
public sealed partial class EditableCellViewModel : ObservableObject
{
    private readonly TableEditorViewModel _editor;

    public EditableCellViewModel(
        TableEditorViewModel editor,
        EditableRowViewModel row,
        EditableColumnViewModel column,
        int ordinal,
        string? value)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Row = row ?? throw new ArgumentNullException(nameof(row));
        Column = column ?? throw new ArgumentNullException(nameof(column));
        Ordinal = ordinal;
        _value = value;
    }

    /// <summary>属する行。書き戻すときの条件は、この行の確定済みの値から組む。</summary>
    public EditableRowViewModel Row { get; }

    /// <summary>属する列。幅と寄せ方をここから引く。</summary>
    public EditableColumnViewModel Column { get; }

    /// <summary>列の並びでの位置。</summary>
    public int Ordinal { get; }

    /// <summary>サーバーに入っているはずの値。null は SQL の NULL。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text))]
    [NotifyPropertyChangedFor(nameof(IsNull))]
    private string? _value;

    /// <summary>編集中の文字列。確定するまで <see cref="Value"/> は動かさない。</summary>
    [ObservableProperty] private string _editText = string.Empty;

    [ObservableProperty] private bool _isEditing;

    /// <summary>直前の書き戻しが通らなかった。次に確定するまで印を残す。</summary>
    [ObservableProperty] private bool _hasError;

    public string Text => Value ?? "NULL";

    public bool IsNull => Value is null;

    public bool IsEditable => Column.IsEditable;

    /// <summary>セルを開く。書き換えられない列では何も起きない。</summary>
    public void BeginEdit()
    {
        if (!IsEditable || IsEditing)
        {
            return;
        }

        // NULL のセルは空欄から始める（「NULL」という文字を消させない。SSMS と同じ）。
        EditText = Value ?? string.Empty;
        IsEditing = true;
    }

    /// <summary>Esc。打ちかけを捨てて元の値へ戻す。</summary>
    public void CancelEdit() => IsEditing = false;

    /// <summary>確定。値が変わっていなければ何も投げない。</summary>
    public Task CommitAsync()
    {
        if (!IsEditing)
        {
            return Task.CompletedTask;
        }

        IsEditing = false;

        var next = Interpret(EditText);

        return string.Equals(next, Value, StringComparison.Ordinal)
            ? Task.CompletedTask
            : _editor.CommitAsync(this, next);
    }

    /// <summary>Ctrl+0。SSMS と同じで、NULL を入れる操作（空欄との区別が付かないため別立てにする）。</summary>
    [RelayCommand]
    public Task SetNullAsync()
    {
        IsEditing = false;

        return !IsEditable || Value is null ? Task.CompletedTask : _editor.CommitAsync(this, null);
    }

    /// <summary>サーバーが受け付けた。表示をその値にする。</summary>
    internal void Accept(string? value)
    {
        Value = value;
        HasError = false;
    }

    /// <summary>サーバーが受け付けなかった。表示は元のまま、印だけを付ける。</summary>
    internal void Reject() => HasError = true;

    /// <summary>
    /// 打ち込まれた文字列を値として読む。
    ///
    /// 空欄は、文字列の列なら空文字列、そうでなければ NULL とする（SSMS と同じ）。
    /// 文字列の列に NULL を入れたいときは Ctrl+0 を使う。
    /// </summary>
    private string? Interpret(string text) => text.Length == 0 && !Column.IsText ? null : text;
}
