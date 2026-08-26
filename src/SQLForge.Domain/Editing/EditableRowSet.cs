namespace SQLForge.Domain.Editing;

/// <summary>
/// 編集グリッドに出す先頭 N 行。
///
/// セルは表示用の文字列で持ち、SQL の NULL は null で表す（<see cref="Query.QueryResultSet"/> と同じ）。
/// 結果グリッドと分けてあるのは、編集には列の素性（鍵か、書き換えられるか）が要るため。
/// </summary>
public sealed class EditableRowSet
{
    public EditableRowSet(
        IReadOnlyList<EditableColumn> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        bool isTruncated = false)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        IsTruncated = isTruncated;
    }

    public IReadOnlyList<EditableColumn> Columns { get; }

    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; }

    /// <summary>上限まで読んだので、まだ続きがある。</summary>
    public bool IsTruncated { get; }

    /// <summary>
    /// 行を 1 件に絞り込める列があるか。無いテーブルは読むだけにする
    /// （どの行を書き換えるのか決められないため）。
    /// </summary>
    public bool HasKey => Columns.Any(column => column.IsKey);

    /// <summary>1 つでも書き換えられる列があるか。</summary>
    public bool HasEditableColumn => Columns.Any(column => !column.IsReadOnly);

    /// <summary>グリッドで値を書き換えられるか。</summary>
    public bool CanEdit => HasKey && HasEditableColumn;
}
