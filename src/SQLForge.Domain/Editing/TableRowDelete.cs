namespace SQLForge.Domain.Editing;

/// <summary>
/// 行 1 つの削除。<see cref="TableCellUpdate"/> と同じで、条件は画面に出ている
/// 変更前の値で組む。行を絞り込めない削除は投げないので、条件が空のものはここで弾く。
/// </summary>
public sealed class TableRowDelete
{
    public TableRowDelete(IReadOnlyList<RowCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.Count == 0)
        {
            throw new ArgumentException("行を特定する条件がありません。", nameof(criteria));
        }

        Criteria = criteria;
    }

    /// <summary>消す行を特定する条件。すべてを AND でつなぐ。</summary>
    public IReadOnlyList<RowCriterion> Criteria { get; }
}
