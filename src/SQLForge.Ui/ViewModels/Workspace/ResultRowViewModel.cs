using System.Globalization;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>グリッドの行 1 つ。</summary>
/// <param name="number">左端に出す行番号。1 から数える。</param>
/// <param name="cells">左から順のセル。</param>
public sealed class ResultRowViewModel(int number, IReadOnlyList<ResultCellViewModel> cells)
{
    /// <summary>行番号。桁区切りは入れない（左端の欄が狭く、番号は読めれば足りるため）。</summary>
    public string Number { get; } = number.ToString(CultureInfo.InvariantCulture);

    public IReadOnlyList<ResultCellViewModel> Cells { get; } = cells;
}
