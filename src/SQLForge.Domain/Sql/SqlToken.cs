namespace SQLForge.Domain.Sql;

/// <summary>
/// 字句 1 つ。文面そのものは持たず位置だけを指す
/// （エディタは 1 文字打つたびに切り直すので、文字列を作らずに済ませたい）。
/// </summary>
/// <param name="Kind">種類。</param>
/// <param name="Offset">文面の先頭からの位置。</param>
/// <param name="Length">文字数。</param>
public readonly record struct SqlToken(SqlTokenKind Kind, int Offset, int Length)
{
    /// <summary>この字句の次の文字の位置。</summary>
    public int End => Offset + Length;

    /// <summary>構文を読むときに飛ばしてよい字句（空白とコメント）か。</summary>
    public bool IsTrivia => Kind is SqlTokenKind.Whitespace or SqlTokenKind.Comment;

    /// <summary>切り出し元の文面から、この字句の文字列を取り出す。</summary>
    public string TextOf(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        return sql.Substring(Offset, Length);
    }
}
