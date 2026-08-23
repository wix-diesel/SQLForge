namespace SQLForge.Domain.Query;

/// <summary>
/// 実行 1 回ぶんの結果。1 つの文面が複数の結果セットを返すことがあるので、
/// 結果セットは並びで持つ（SSMS が「結果 1」「結果 2」と並べるのと同じ）。
/// </summary>
public sealed class QueryResult
{
    public QueryResult(IReadOnlyList<QueryResultSet> resultSets, int recordsAffected, TimeSpan elapsed)
    {
        ResultSets = resultSets ?? throw new ArgumentNullException(nameof(resultSets));
        RecordsAffected = recordsAffected;
        Elapsed = elapsed;
    }

    public IReadOnlyList<QueryResultSet> ResultSets { get; }

    /// <summary>影響を受けた行数。読み取りだけの文面など、取れないときは -1。</summary>
    public int RecordsAffected { get; }

    /// <summary>実行にかかった時間。送ってから読み終えるまでを測る。</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>返ってきた行の合計。</summary>
    public int TotalRows => ResultSets.Sum(set => set.Rows.Count);
}
