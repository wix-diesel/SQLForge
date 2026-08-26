using System.Globalization;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>編集グリッドの行 1 つ。</summary>
public sealed class EditableRowViewModel
{
    public EditableRowViewModel(
        TableEditorViewModel editor,
        int number,
        IReadOnlyList<EditableColumnViewModel> columns,
        IReadOnlyList<string?> values)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(values);

        Number = number.ToString(CultureInfo.InvariantCulture);

        var cells = new List<EditableCellViewModel>(columns.Count);

        for (var ordinal = 0; ordinal < columns.Count; ordinal++)
        {
            // 列が足りない行が来ても落ちないようにする（本来は起きない）。
            var value = ordinal < values.Count ? values[ordinal] : null;

            cells.Add(new EditableCellViewModel(editor, this, columns[ordinal], ordinal, value));
        }

        Cells = cells;
    }

    /// <summary>行番号。1 から数える。</summary>
    public string Number { get; }

    public IReadOnlyList<EditableCellViewModel> Cells { get; }

    /// <summary>
    /// 確定済みの値の並び。書き戻すときに「サーバーに入っているはずの行」として条件に使う。
    /// </summary>
    public IReadOnlyList<string?> Values => Cells.Select(cell => cell.Value).ToList();
}
