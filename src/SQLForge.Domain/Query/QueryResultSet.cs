namespace SQLForge.Domain.Query;

/// <summary>
/// 結果セット 1 つ。
///
/// セルは表示用の文字列で持ち、SQL の NULL は null で表す。空文字列と NULL は
/// 見た目が同じでも意味が違うので、「NULL」という文字列に潰さずここで区別しておく
/// （値そのものを文字列へ写すのはドライバー側の責務）。
/// </summary>
public sealed class QueryResultSet
{
    public QueryResultSet(
        IReadOnlyList<QueryColumn> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        bool isTruncated = false)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        IsTruncated = isTruncated;
    }

    public IReadOnlyList<QueryColumn> Columns { get; }

    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; }

    /// <summary>取得上限に達したので途中で打ち切った。まだ続きがあることを画面に出す。</summary>
    public bool IsTruncated { get; }
}
