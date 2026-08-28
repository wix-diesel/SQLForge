namespace SQLForge.Domain.Sql;

/// <summary>
/// キャレットの周りを読んで、何を補完すべきかを決める。
///
/// 構文解析器は持たない。字句の並びを後ろへ辿って直前の句を見るだけで、
/// モックアップが求める精度（FROM の別名を解いて列を出す）には届く。
/// </summary>
public static class SqlCompletionAnalyzer
{
    /// <summary>この語の後ろはテーブルを書く場所。</summary>
    private static readonly HashSet<string> TableClauses = new(StringComparer.Ordinal)
    {
        "FROM", "JOIN", "INTO", "UPDATE", "APPLY", "TABLE"
    };

    /// <summary>この語の後ろは列を書く場所。</summary>
    private static readonly HashSet<string> ColumnClauses = new(StringComparer.Ordinal)
    {
        "SELECT", "WHERE", "ON", "SET", "BY", "GROUP", "ORDER", "HAVING", "AND", "OR",
        "CASE", "WHEN", "THEN", "ELSE", "VALUES", "IN", "NOT", "LIKE", "BETWEEN"
    };

    /// <summary>テーブル名がこの後ろに並ぶ語。別名の解決で読むのはここだけ。</summary>
    private static readonly HashSet<string> TableIntroducers = new(StringComparer.Ordinal)
    {
        "FROM", "JOIN", "INTO", "UPDATE", "APPLY"
    };

    /// <summary>文面とキャレットの位置から、補完に要ることを読み取る。</summary>
    public static SqlCompletionContext Analyze(string sql, int caret)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var position = Math.Clamp(caret, 0, sql.Length);
        var all = SqlLexer.Tokenize(sql);

        // 文字列やコメントの中では出さない。'2026-01-01' を打っている最中に
        // テーブル名を並べても邪魔にしかならない。
        if (IsInsideLiteral(all, position))
        {
            return SqlCompletionContext.None;
        }

        var words = all.Where(token => !token.IsTrivia).ToList();
        var (replaceOffset, prefix) = ReadPrefix(sql, words, position);
        var before = LastIndexBefore(words, replaceOffset);
        var qualifier = ReadQualifier(sql, words, before);
        var (statementStart, statementEnd) = StatementRange(sql, words, before);

        return new SqlCompletionContext(
            KindOf(sql, words, before, statementStart, qualifier),
            prefix,
            replaceOffset,
            position - replaceOffset,
            qualifier,
            TableReferences(sql, words, statementStart, statementEnd));
    }

    /// <summary>
    /// 文字列やコメントの中にキャレットが居るか。閉じていない文字列や行コメントは
    /// 末尾までが 1 つの字句なので、末尾ちょうども中と見なす。
    /// </summary>
    private static bool IsInsideLiteral(IReadOnlyList<SqlToken> tokens, int position) =>
        tokens.Any(token =>
            (token.Kind is SqlTokenKind.String or SqlTokenKind.Comment)
            && token.Offset < position
            && position <= token.End);

    /// <summary>打ちかけの語と、それを置き換える範囲の先頭を返す。</summary>
    private static (int Offset, string Prefix) ReadPrefix(string sql, List<SqlToken> words, int position)
    {
        var index = words.FindIndex(token => token.Offset < position && position <= token.End);

        if (index < 0 || !IsWord(words[index]))
        {
            return (position, string.Empty);
        }

        var offset = words[index].Offset;

        // [注文 のように引用符を開いた途中なら、絞り込みには中身だけを使う。
        return (offset, TrimOpeningQuote(sql[offset..position]));
    }

    private static string TrimOpeningQuote(string text) =>
        text.Length > 0 && text[0] is '[' or '"' or '`' ? text[1..] : text;

    private static bool IsWord(SqlToken token) =>
        token.Kind is SqlTokenKind.Identifier or SqlTokenKind.Keyword
            or SqlTokenKind.Function or SqlTokenKind.Type or SqlTokenKind.Variable;

    /// <summary>名前として読める字句か。予約語は句の切れ目とまぎれるので外す。</summary>
    private static bool IsName(SqlToken token) =>
        token.Kind is SqlTokenKind.Identifier or SqlTokenKind.Function or SqlTokenKind.Type;

    private static int LastIndexBefore(List<SqlToken> words, int offset)
    {
        var index = words.Count - 1;
        while (index >= 0 && words[index].End > offset)
        {
            index--;
        }

        return index;
    }

    /// <summary>直前が . なら、その左の名前を返す（別名・テーブル名・スキーマ名のどれか）。</summary>
    private static string? ReadQualifier(string sql, List<SqlToken> words, int before)
    {
        if (before < 1 || Text(sql, words[before]) != ".")
        {
            return null;
        }

        var owner = words[before - 1];

        return IsName(owner) || owner.Kind == SqlTokenKind.Keyword
            ? SqlIdentifierText.Unquote(Text(sql, owner))
            : null;
    }

    /// <summary>キャレットが居る文の範囲。; と GO で区切る。</summary>
    private static (int Start, int End) StatementRange(string sql, List<SqlToken> words, int before)
    {
        var start = 0;
        for (var index = before; index >= 0; index--)
        {
            if (IsStatementBreak(sql, words[index]))
            {
                start = index + 1;
                break;
            }
        }

        var end = words.Count;
        for (var index = before + 1; index < words.Count; index++)
        {
            if (IsStatementBreak(sql, words[index]))
            {
                end = index;
                break;
            }
        }

        return (start, end);
    }

    private static bool IsStatementBreak(string sql, SqlToken token) =>
        Text(sql, token) == ";"
        || (token.Kind == SqlTokenKind.Keyword && Word(sql, token) == "GO");

    private static SqlCompletionKind KindOf(
        string sql, List<SqlToken> words, int before, int statementStart, string? qualifier)
    {
        // 修飾が付いているときは列を第一候補にする（スキーマ名だった場合の
        // 読み替えは、カタログを知っている Application 側でやる）。
        if (qualifier is not null)
        {
            return SqlCompletionKind.Column;
        }

        for (var index = before; index >= statementStart; index--)
        {
            if (words[index].Kind != SqlTokenKind.Keyword)
            {
                continue;
            }

            var word = Word(sql, words[index]);

            if (TableClauses.Contains(word))
            {
                return SqlCompletionKind.Table;
            }

            if (ColumnClauses.Contains(word))
            {
                return SqlCompletionKind.Column;
            }
        }

        return SqlCompletionKind.Keyword;
    }

    /// <summary>文の中の FROM / JOIN などを拾って、テーブルと別名の対応を作る。</summary>
    private static IReadOnlyList<SqlTableReference> TableReferences(
        string sql, List<SqlToken> words, int start, int end)
    {
        var references = new List<SqlTableReference>();
        var index = start;

        while (index < end)
        {
            if (words[index].Kind == SqlTokenKind.Keyword && TableIntroducers.Contains(Word(sql, words[index])))
            {
                index = ReadTableList(sql, words, index + 1, end, references);
                continue;
            }

            index++;
        }

        return references;
    }

    /// <summary>カンマ区切りのテーブルの並びを読む。読めなくなった位置を返す。</summary>
    private static int ReadTableList(
        string sql, List<SqlToken> words, int index, int end, List<SqlTableReference> references)
    {
        while (true)
        {
            var next = ReadTableReference(sql, words, index, end, references);
            if (next == index)
            {
                return index + 1;
            }

            index = next;

            if (index >= end || Text(sql, words[index]) != ",")
            {
                return index;
            }

            index++;
        }
    }

    private static int ReadTableReference(
        string sql, List<SqlToken> words, int index, int end, List<SqlTableReference> references)
    {
        if (index >= end || !IsName(words[index]))
        {
            return index;
        }

        var parts = new List<string> { SqlIdentifierText.Unquote(Text(sql, words[index])) };
        index++;

        while (index + 1 < end && Text(sql, words[index]) == "." && IsName(words[index + 1]))
        {
            parts.Add(SqlIdentifierText.Unquote(Text(sql, words[index + 1])));
            index += 2;
        }

        references.Add(new SqlTableReference(
            parts.Count > 1 ? parts[^2] : null,
            parts[^1],
            ReadAlias(sql, words, ref index, end)));

        return index;
    }

    /// <summary>テーブル名に続く別名。AS は付いていてもいなくてもよい。</summary>
    private static string? ReadAlias(string sql, List<SqlToken> words, ref int index, int end)
    {
        var candidate = index;

        if (candidate < end
            && words[candidate].Kind == SqlTokenKind.Keyword
            && Word(sql, words[candidate]) == "AS")
        {
            candidate++;
        }

        if (candidate >= end || words[candidate].Kind != SqlTokenKind.Identifier)
        {
            return null;
        }

        var alias = SqlIdentifierText.Unquote(Text(sql, words[candidate]));
        index = candidate + 1;

        return alias;
    }

    private static string Text(string sql, SqlToken token) => token.TextOf(sql);

    private static string Word(string sql, SqlToken token) => token.TextOf(sql).ToUpperInvariant();
}
