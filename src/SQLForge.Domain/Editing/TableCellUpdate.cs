namespace SQLForge.Domain.Editing;

/// <summary>
/// セル 1 つの書き換え。SSMS の編集グリッドと同じで、確定するたびに 1 セルずつ送る。
///
/// 条件は変更前の値で組む。行を絞り込めない更新は投げないので、
/// 条件が空のものはここで弾く。
/// </summary>
public sealed class TableCellUpdate
{
    public TableCellUpdate(string column, string? value, IReadOnlyList<RowCriterion> criteria)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            throw new ArgumentException("書き換える列名は空にできません。", nameof(column));
        }

        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.Count == 0)
        {
            throw new ArgumentException("行を特定する条件がありません。", nameof(criteria));
        }

        Column = column;
        Value = value;
        Criteria = criteria;
    }

    /// <summary>書き換える列。</summary>
    public string Column { get; }

    /// <summary>新しい値。null は SQL の NULL。</summary>
    public string? Value { get; }

    /// <summary>行を特定する条件。すべてを AND でつなぐ。</summary>
    public IReadOnlyList<RowCriterion> Criteria { get; }
}
