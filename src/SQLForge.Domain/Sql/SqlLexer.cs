namespace SQLForge.Domain.Sql;

/// <summary>
/// SQL の文面を字句へ切る。色分け・補完・整形はすべてこの 1 本を通す
/// （同じ文面を 2 通りに読む実装が並ぶと、必ずどこかで食い違うため）。
///
/// 構文解析はしない。閉じていない文字列やコメントは末尾までを 1 つの字句として返し、
/// 例外は投げない（書きかけの文面をそのまま色分けするため）。
/// </summary>
public static class SqlLexer
{
    /// <summary>2 文字で 1 つに読む演算子。1 文字ずつに割ると整形で間に空白が入ってしまう。</summary>
    private static readonly string[] TwoCharOperators =
    [
        "<=", ">=", "<>", "!=", "!<", "!>", "||", "::", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^="
    ];

    /// <summary>文面を先頭から最後まで字句へ切る。空白もコメントも落とさずに返す。</summary>
    public static IReadOnlyList<SqlToken> Tokenize(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var tokens = new List<SqlToken>();
        var index = 0;

        while (index < sql.Length)
        {
            var token = Next(sql, index);
            tokens.Add(token);
            index = token.End;
        }

        return tokens;
    }

    /// <summary>位置 <paramref name="start"/> から字句を 1 つ読む。必ず 1 文字以上進む。</summary>
    private static SqlToken Next(string sql, int start)
    {
        var current = sql[start];

        if (char.IsWhiteSpace(current))
        {
            return new SqlToken(SqlTokenKind.Whitespace, start, RunLength(sql, start, char.IsWhiteSpace));
        }

        if (current == '-' && At(sql, start + 1) == '-')
        {
            return new SqlToken(SqlTokenKind.Comment, start, LineCommentLength(sql, start));
        }

        if (current == '/' && At(sql, start + 1) == '*')
        {
            return new SqlToken(SqlTokenKind.Comment, start, BlockCommentLength(sql, start));
        }

        return NextLiteralOrWord(sql, start, current);
    }

    private static SqlToken NextLiteralOrWord(string sql, int start, char current)
    {
        if (current == '\'')
        {
            return new SqlToken(SqlTokenKind.String, start, QuotedLength(sql, start, '\''));
        }

        // N'...' は 1 つの文字列リテラル。頭の N を識別子として切り離さない。
        if ((current is 'N' or 'n') && At(sql, start + 1) == '\'')
        {
            return new SqlToken(SqlTokenKind.String, start, 1 + QuotedLength(sql, start + 1, '\''));
        }

        if (current is '"' or '`')
        {
            return new SqlToken(SqlTokenKind.Identifier, start, QuotedLength(sql, start, current));
        }

        if (current == '[')
        {
            return new SqlToken(SqlTokenKind.Identifier, start, QuotedLength(sql, start, ']'));
        }

        if (current == '@')
        {
            return new SqlToken(SqlTokenKind.Variable, start, VariableLength(sql, start));
        }

        if (char.IsDigit(current) || (current == '.' && char.IsDigit(At(sql, start + 1))))
        {
            return new SqlToken(SqlTokenKind.Number, start, NumberLength(sql, start));
        }

        if (IsWordStart(current))
        {
            var length = RunLength(sql, start, IsWordPart);
            return new SqlToken(SqlKeywords.Classify(sql.Substring(start, length)), start, length);
        }

        return new SqlToken(SqlTokenKind.Punctuation, start, OperatorLength(sql, start));
    }

    /// <summary>識別子の頭に来られる文字。# は T-SQL の一時テーブル。</summary>
    private static bool IsWordStart(char value) => char.IsLetter(value) || value is '_' or '#';

    private static bool IsWordPart(char value) => char.IsLetterOrDigit(value) || value is '_' or '#' or '$';

    private static char At(string sql, int index) => index >= 0 && index < sql.Length ? sql[index] : '\0';

    private static int RunLength(string sql, int start, Func<char, bool> accepts)
    {
        var index = start;
        while (index < sql.Length && accepts(sql[index]))
        {
            index++;
        }

        return index - start;
    }

    /// <summary>-- から行末まで。改行そのものは含めない（次の空白の字句が受け持つ）。</summary>
    private static int LineCommentLength(string sql, int start)
    {
        var index = start + 2;
        while (index < sql.Length && sql[index] is not ('\n' or '\r'))
        {
            index++;
        }

        return index - start;
    }

    /// <summary>/* から */ まで。T-SQL は入れ子にできるので深さを数える。</summary>
    private static int BlockCommentLength(string sql, int start)
    {
        var index = start + 2;
        var depth = 1;

        while (index < sql.Length && depth > 0)
        {
            if (sql[index] == '/' && At(sql, index + 1) == '*')
            {
                depth++;
                index += 2;
            }
            else if (sql[index] == '*' && At(sql, index + 1) == '/')
            {
                depth--;
                index += 2;
            }
            else
            {
                index++;
            }
        }

        return index - start;
    }

    /// <summary>
    /// 引用符で囲まれた字句の長さ。閉じ記号を 2 つ続けた形（'' や ]]）は
    /// 中身の 1 文字として読み飛ばす。閉じていなければ末尾までを返す。
    /// </summary>
    private static int QuotedLength(string sql, int start, char close)
    {
        var index = start + 1;

        while (index < sql.Length)
        {
            if (sql[index] != close)
            {
                index++;
                continue;
            }

            if (At(sql, index + 1) == close)
            {
                index += 2;
                continue;
            }

            return index + 1 - start;
        }

        return sql.Length - start;
    }

    private static int VariableLength(string sql, int start)
    {
        var index = start + 1;

        // @@ROWCOUNT のようなシステム変数。
        if (At(sql, index) == '@')
        {
            index++;
        }

        while (index < sql.Length && IsWordPart(sql[index]))
        {
            index++;
        }

        return index - start;
    }

    private static int NumberLength(string sql, int start)
    {
        if (sql[start] == '0' && At(sql, start + 1) is 'x' or 'X')
        {
            var hex = start + 2;
            while (hex < sql.Length && Uri.IsHexDigit(sql[hex]))
            {
                hex++;
            }

            return hex - start;
        }

        var index = start + RunLength(sql, start, char.IsDigit);

        if (At(sql, index) == '.')
        {
            index++;
            index += RunLength(sql, index, char.IsDigit);
        }

        return ExponentEnd(sql, index) - start;
    }

    /// <summary>1e10 のような指数部。数字が続かないときは読まない（1e は 1 と e に分かれる）。</summary>
    private static int ExponentEnd(string sql, int index)
    {
        if (At(sql, index) is not ('e' or 'E'))
        {
            return index;
        }

        var digits = index + 1;
        if (At(sql, digits) is '+' or '-')
        {
            digits++;
        }

        if (!char.IsDigit(At(sql, digits)))
        {
            return index;
        }

        return digits + RunLength(sql, digits, char.IsDigit);
    }

    private static int OperatorLength(string sql, int start)
    {
        if (start + 1 >= sql.Length)
        {
            return 1;
        }

        var pair = sql.AsSpan(start, 2);
        foreach (var candidate in TwoCharOperators)
        {
            if (pair.SequenceEqual(candidate))
            {
                return 2;
            }
        }

        return 1;
    }
}
