namespace SQLForge.Domain.Editing;

/// <summary>
/// 行 1 つの追加。SSMS の編集グリッドと同じで、グリッドのいちばん下にある
/// 新しい行（行番号が <c>*</c> の行）を確定したときに 1 行ずつ送る。
///
/// 並ぶのは打ち込まれた列だけ。触っていない列は文面に出さず、既定値と NULL の判断は
/// サーバーに任せる。
/// </summary>
public sealed class TableRowInsert
{
    public TableRowInsert(IReadOnlyList<TableCellValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            throw new ArgumentException("追加する値がありません。", nameof(values));
        }

        // 同じ列が 2 度並ぶと壊れた文面になる（INSERT の列並びに同じ名前が出る）。
        if (values.Select(value => value.Column).Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new ArgumentException("同じ列に 2 つの値は置けません。", nameof(values));
        }

        Values = values;
    }

    /// <summary>置く値。列名は重ならない。</summary>
    public IReadOnlyList<TableCellValue> Values { get; }
}
