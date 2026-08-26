using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Editing;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 編集グリッドの行 1 つ。
///
/// いちばん下の 1 行だけは「新しい行」で、行番号の代わりに <c>*</c> を出す（SSMS と同じ）。
/// そこへ打ち込んで確定すると 1 行が足され、その行は普通の行になって、下に新しい行がまた出る。
/// </summary>
public sealed partial class EditableRowViewModel : ObservableObject
{
    private readonly TableEditorViewModel _editor;

    public EditableRowViewModel(
        TableEditorViewModel editor,
        int number,
        IReadOnlyList<EditableColumnViewModel> columns,
        IReadOnlyList<string?> values,
        bool isNewRow = false)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(values);

        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _isNewRow = isNewRow;
        _number = isNewRow ? NewRowMark : Format(number);

        var cells = new List<EditableCellViewModel>(columns.Count);

        for (var ordinal = 0; ordinal < columns.Count; ordinal++)
        {
            // 列が足りない行が来ても落ちないようにする（本来は起きない）。
            var value = ordinal < values.Count ? values[ordinal] : null;

            cells.Add(new EditableCellViewModel(editor, this, columns[ordinal], ordinal, value, !isNewRow));
        }

        Cells = cells;
    }

    /// <summary>新しい行の行番号。SSMS の編集グリッドと同じ印。</summary>
    public const string NewRowMark = "*";

    /// <summary>行番号。1 から数える。新しい行は <c>*</c>。</summary>
    [ObservableProperty] private string _number;

    /// <summary>いちばん下の「これから足す行」か。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    private bool _isNewRow;

    public IReadOnlyList<EditableCellViewModel> Cells { get; }

    /// <summary>
    /// 確定済みの値の並び。書き戻すときに「サーバーに入っているはずの行」として条件に使う。
    /// </summary>
    public IReadOnlyList<string?> Values => Cells.Select(cell => cell.Value).ToList();

    /// <summary>打ち込まれた値だけ。行を足す文面に並べるのはこれ（触っていない列は既定値に任せる）。</summary>
    public IReadOnlyList<TableCellValue> AssignedValues =>
        Cells.Where(cell => cell.IsAssigned)
            .Select(cell => new TableCellValue(cell.Column.Name, cell.Value))
            .ToList();

    /// <summary>新しい行に打ちかけがあるか。</summary>
    public bool HasPendingValues => IsNewRow && Cells.Any(cell => cell.IsAssigned);

    /// <summary>右クリックの「行の削除」を出すか。</summary>
    public bool CanDelete => !IsNewRow && _editor.CanDelete;

    /// <summary>新しい行を確定する。既存の行では何も起きない。</summary>
    public Task CommitAsync() => IsNewRow ? _editor.CommitNewRowAsync(this) : Task.CompletedTask;

    /// <summary>右クリックの「行の削除」。確認を取るのは <see cref="TableEditorViewModel"/> 側。</summary>
    [RelayCommand]
    private Task DeleteAsync() => _editor.DeleteRowAsync(this);

    /// <summary>右クリックの「新しい行を取り消す」。打ちかけを捨てる。</summary>
    [RelayCommand]
    private void Cancel() => Reset();

    /// <summary>足し終わった行を普通の行にする。</summary>
    internal void MarkCommitted(int number)
    {
        IsNewRow = false;
        Number = Format(number);

        foreach (var cell in Cells)
        {
            cell.NotifyEditable();
        }
    }

    /// <summary>行番号を振り直す（行を消したあと）。</summary>
    internal void Renumber(int number) => Number = Format(number);

    /// <summary>打ちかけを捨てて、触っていない新しい行へ戻す。</summary>
    internal void Reset()
    {
        if (!IsNewRow)
        {
            return;
        }

        foreach (var cell in Cells)
        {
            cell.Clear();
        }
    }

    /// <summary>足したあとにサーバーから読み直した値を写す。</summary>
    internal void Apply(IReadOnlyList<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        for (var ordinal = 0; ordinal < Cells.Count && ordinal < values.Count; ordinal++)
        {
            Cells[ordinal].Accept(values[ordinal]);
        }
    }

    private static string Format(int number) => number.ToString(CultureInfo.InvariantCulture);
}
