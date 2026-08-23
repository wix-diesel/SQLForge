using SQLForge.Domain.Query;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 結果セット 1 つを表に組み直したもの。
///
/// 列も行も実行のたびに丸ごと作り直す。途中で差し替わらないので、
/// 変更通知は持たない（1,000 行ぶんの通知を配ると、そのほうが重い）。
/// </summary>
public sealed class QueryResultSetViewModel
{
    public QueryResultSetViewModel(QueryResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);

        var columns = new List<ResultColumnViewModel>(resultSet.Columns.Count);

        for (var ordinal = 0; ordinal < resultSet.Columns.Count; ordinal++)
        {
            var column = resultSet.Columns[ordinal];
            var position = ordinal;

            columns.Add(new ResultColumnViewModel(
                column,
                ResultColumnWidth.For(
                    column.Name,
                    column.TypeName,
                    resultSet.Rows.Select(row => ValueAt(row, position)))));
        }

        var rows = new List<ResultRowViewModel>(resultSet.Rows.Count);

        for (var index = 0; index < resultSet.Rows.Count; index++)
        {
            var row = resultSet.Rows[index];
            var cells = new List<ResultCellViewModel>(columns.Count);

            for (var ordinal = 0; ordinal < columns.Count; ordinal++)
            {
                cells.Add(new ResultCellViewModel(columns[ordinal], ValueAt(row, ordinal)));
            }

            rows.Add(new ResultRowViewModel(index + 1, cells));
        }

        Columns = columns;
        Rows = rows;
        IsTruncated = resultSet.IsTruncated;
    }

    public IReadOnlyList<ResultColumnViewModel> Columns { get; }

    public IReadOnlyList<ResultRowViewModel> Rows { get; }

    public int RowCount => Rows.Count;

    /// <summary>取得上限で打ち切った。まだ続きがあることを画面に出す。</summary>
    public bool IsTruncated { get; }

    /// <summary>列が足りない行が来ても落ちないようにする（本来は起きない）。</summary>
    private static string? ValueAt(IReadOnlyList<string?> row, int ordinal) =>
        ordinal < row.Count ? row[ordinal] : null;
}
