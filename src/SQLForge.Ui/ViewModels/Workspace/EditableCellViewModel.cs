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
///
/// いちばん下の新しい行（行番号が <c>*</c> の行）だけは、確定してもサーバーへは送らない。
/// そちらは行がそろってから 1 行として足すので、値を手元へ置くだけにする。
/// </summary>
public sealed partial class EditableCellViewModel : ObservableObject
{
    private readonly TableEditorViewModel _editor;

    public EditableCellViewModel(
        TableEditorViewModel editor,
        EditableRowViewModel row,
        EditableColumnViewModel column,
        int ordinal,
        string? value,
        bool isAssigned = true)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Row = row ?? throw new ArgumentNullException(nameof(row));
        Column = column ?? throw new ArgumentNullException(nameof(column));
        Ordinal = ordinal;
        _value = value;
        _isAssigned = isAssigned;
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

    /// <summary>
    /// 値が決まっているか。新しい行で、まだ触っていないセルだけが false になる。
    /// 触っていない列は INSERT の文面に出さない（サーバーの既定値を効かせるため）。
    /// </summary>
    [ObservableProperty] private bool _isAssigned;

    public string Text => Value ?? "NULL";

    public bool IsNull => Value is null;

    /// <summary>新しい行では「打ち込めるか」、既存の行では「書き換えられるか」を見る。</summary>
    public bool IsEditable => Row.IsNewRow ? Column.IsInsertable : Column.IsEditable;

    /// <summary>セルを開く。書き換えられない列では何も起きない。</summary>
    public void BeginEdit()
    {
        if (!IsEditable || IsEditing)
        {
            return;
        }

        // ほかの行へ移ったら、打ちかけの新しい行はそこで確定する（SSMS と同じ）。
        _editor.LeaveRow(Row);

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

        // 新しい行はまだサーバーへ送らない。行として確定するときに 1 行で足す。
        if (Row.IsNewRow)
        {
            // 空欄のまま通り過ぎただけのセルは触っていない扱いにする（SSMS と同じ）。
            // ここで空文字列を置くと、サーバーの既定値が効かなくなる。
            if (IsAssigned || EditText.Length > 0)
            {
                Assign(next);
            }

            return Task.CompletedTask;
        }

        return string.Equals(next, Value, StringComparison.Ordinal)
            ? Task.CompletedTask
            : _editor.CommitAsync(this, next);
    }

    /// <summary>Ctrl+0。SSMS と同じで、NULL を入れる操作（空欄との区別が付かないため別立てにする）。</summary>
    [RelayCommand]
    public Task SetNullAsync()
    {
        IsEditing = false;

        if (!IsEditable)
        {
            return Task.CompletedTask;
        }

        if (Row.IsNewRow)
        {
            Assign(null);
            return Task.CompletedTask;
        }

        return Value is null ? Task.CompletedTask : _editor.CommitAsync(this, null);
    }

    /// <summary>サーバーが受け付けた。表示をその値にする。</summary>
    internal void Accept(string? value)
    {
        Value = value;
        IsAssigned = true;
        HasError = false;
    }

    /// <summary>サーバーが受け付けなかった。表示は元のまま、印だけを付ける。</summary>
    internal void Reject() => HasError = true;

    /// <summary>新しい行のセルに値を置く。サーバーへは送らない。</summary>
    internal void Assign(string? value)
    {
        Value = value;
        IsAssigned = true;
        HasError = false;

        _editor.NotifyRowPending();
    }

    /// <summary>新しい行を取り消す。触っていない状態へ戻す。</summary>
    internal void Clear()
    {
        IsEditing = false;
        Value = null;
        IsAssigned = false;
        HasError = false;
    }

    /// <summary>
    /// 行の素性（新しい行かどうか）が変わったことを伝える。
    /// 足したばかりの行は、そこから先は既存の行として書き換えられる。
    /// </summary>
    internal void NotifyEditable() => OnPropertyChanged(nameof(IsEditable));

    /// <summary>
    /// 打ち込まれた文字列を値として読む。
    ///
    /// 空欄は、文字列の列なら空文字列、そうでなければ NULL とする（SSMS と同じ）。
    /// 文字列の列に NULL を入れたいときは Ctrl+0 を使う。
    /// </summary>
    private string? Interpret(string text) => text.Length == 0 && !Column.IsText ? null : text;
}
