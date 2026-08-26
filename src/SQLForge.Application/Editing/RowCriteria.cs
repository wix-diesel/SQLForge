using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// 画面の 1 行から「サーバーのこの行」を指す条件を組む。書き換えと削除で同じ組み方を使う
/// （どちらも間違えると、画面に出ていない行まで巻き込む）。
/// </summary>
internal static class RowCriteria
{
    /// <summary>
    /// 鍵になる列の、変更前の値だけを並べる。1 つも無ければ、どの行を指すのか決められない。
    /// </summary>
    public static IReadOnlyList<RowCriterion> From(
        IReadOnlyList<EditableColumn> columns,
        IReadOnlyList<string?> row)
    {
        EnsureShape(columns, row);

        var criteria = new List<RowCriterion>();

        for (var ordinal = 0; ordinal < columns.Count; ordinal++)
        {
            if (columns[ordinal].IsKey)
            {
                criteria.Add(new RowCriterion(columns[ordinal].Name, row[ordinal]));
            }
        }

        return criteria.Count > 0
            ? criteria
            : throw new TableEditRejectedException(
                "行を 1 件に特定できないため、この行は編集できません（主キーがありません）。");
    }

    /// <summary>行の値の並びが列の並びと合っているか。合わないときは読み直してもらう。</summary>
    public static void EnsureShape(IReadOnlyList<EditableColumn> columns, IReadOnlyList<string?> row)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(row);

        if (columns.Count == 0 || row.Count != columns.Count)
        {
            throw new TableEditRejectedException("行の形が列の並びと合いません。読み直してください。");
        }
    }
}
