using System.Text;

namespace SQLForge.Domain.Sql;

/// <summary>
/// SQL の文面を整える。<see cref="SqlLexer"/> が切った字句を並べ直すだけで、
/// 生の文字列は書き換えない（文字列やコメントの中の SELECT を壊さないため）。
///
/// 保つ約束は 2 つ。
/// <list type="bullet">
///   <item>字句の並びは変えない。増やさない・減らさない・つなげない（変わるのは空白と予約語の大小だけ）。</item>
///   <item>2 回かけても結果は変わらない（べき等）。</item>
/// </list>
/// </summary>
public static class SqlFormatter
{
    /// <summary>字下げ 1 段ぶん。</summary>
    public const string Indent = "    ";

    /// <summary>行の頭へ出す予約語。ここに載っている語で改行する。</summary>
    private static readonly HashSet<string> ClauseStarters = new(StringComparer.Ordinal)
    {
        "SELECT", "FROM", "WHERE", "GROUP", "HAVING", "ORDER", "UNION", "EXCEPT", "INTERSECT",
        "INSERT", "UPDATE", "DELETE", "VALUES", "SET", "INNER", "LEFT", "RIGHT", "FULL", "CROSS",
        "JOIN", "BEGIN", "END", "DECLARE", "EXEC", "EXECUTE", "CREATE", "ALTER", "DROP",
        "TRUNCATE", "WITH", "GO", "USE", "IF", "WHILE", "RETURN", "COMMIT", "ROLLBACK", "PRINT",
        "MERGE", "OPTION", "OFFSET", "FETCH", "GRANT", "REVOKE", "DENY", "RAISERROR", "THROW",
        "WAITFOR"
    };

    /// <summary>JOIN の前に付く語。LEFT OUTER JOIN を 3 行に割らないために見る。</summary>
    private static readonly HashSet<string> JoinModifiers = new(StringComparer.Ordinal)
    {
        "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "OUTER"
    };

    /// <summary>文面 1 つを整える。空の文面は空のまま返す。</summary>
    public static string Format(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        return new Formatter(sql).Run();
    }

    /// <summary>字句を 1 つずつ書き出す。改行と空白の判断に要る状態はここが持つ。</summary>
    private sealed class Formatter
    {
        private readonly List<SqlToken> _tokens = [];
        private readonly List<string> _texts = [];
        private readonly List<string> _upper = [];
        private readonly StringBuilder _builder = new();

        /// <summary>項目が 2 つ以上ある並び（SELECT の列など）を開く語の位置。</summary>
        private readonly HashSet<int> _listOpeners = [];

        private int _parenDepth;
        private int _caseDepth;
        private bool _pendingBlankLine;
        private bool _forceNewLine;
        private bool _betweenPending;

        internal Formatter(string sql)
        {
            foreach (var token in SqlLexer.Tokenize(sql))
            {
                if (token.Kind == SqlTokenKind.Whitespace)
                {
                    continue;
                }

                var text = token.TextOf(sql);
                _tokens.Add(token);
                _texts.Add(text);
                _upper.Add(text.ToUpperInvariant());
            }

            MarkLists();
        }

        internal string Run()
        {
            for (var index = 0; index < _tokens.Count; index++)
            {
                Write(index);
            }

            return _builder.ToString().TrimEnd();
        }

        private void Write(int index)
        {
            // 先頭の字句は改行も字下げもしない。
            if (_builder.Length > 0)
            {
                if (StartsLine(index))
                {
                    NewLine(IndentFor(index));
                }
                else if (NeedsSpace(index))
                {
                    _builder.Append(' ');
                }
            }

            // 予約語だけ大文字へ揃える。識別子は照合順序によっては大小を区別するので触らない。
            _builder.Append(_tokens[index].Kind == SqlTokenKind.Keyword ? _upper[index] : _texts[index]);
            Track(index);
        }

        private bool StartsLine(int index)
        {
            // 直前が行コメント・; ・GO のときは、改行しないと文面の意味が変わる。
            if (_forceNewLine)
            {
                return true;
            }

            // 括弧の中は折らない。関数呼び出しや IN (...) を縦に割らないため。
            if (_parenDepth > 0)
            {
                return false;
            }

            if (_texts[index] == ";")
            {
                return false;
            }

            return IsClauseStart(index)
                || (index > 0 && (_texts[index - 1] == "," || _listOpeners.Contains(index - 1)))
                || IsBooleanConnector(index);
        }

        /// <summary>
        /// 縦に並べる値打ちのある並びを見つけておく。SELECT の列が 1 つだけなら
        /// 折らずに 1 行へ収める（SELECT TOP (1000) * のような短い文面を膨らませないため）。
        /// </summary>
        private void MarkLists()
        {
            for (var index = 0; index < _tokens.Count; index++)
            {
                if (OpensList(index) && HasSeparatedItems(index))
                {
                    _listOpeners.Add(index);
                }
            }
        }

        private bool OpensList(int index) =>
            _tokens[index].Kind == SqlTokenKind.Keyword && _upper[index] is "SELECT" or "BY" or "SET";

        /// <summary>この語が開く並びに、括弧の外のカンマがあるか（次の句か文の終わりまでを見る）。</summary>
        private bool HasSeparatedItems(int start)
        {
            var depth = 0;
            var caseDepth = 0;

            for (var index = start + 1; index < _tokens.Count; index++)
            {
                var text = _texts[index];

                if (text == "(")
                {
                    depth++;
                }
                else if (text == ")")
                {
                    if (depth == 0)
                    {
                        return false;
                    }

                    depth--;
                }
                else if (depth == 0 && !ScanClause(index, ref caseDepth, out var separated))
                {
                    return separated;
                }
            }

            return false;
        }

        /// <summary>
        /// 並びの走査を 1 語ぶん進める。続けてよければ true。
        /// 打ち切るときは <paramref name="separated"/> に答え（カンマを見つけたか）を入れる。
        /// </summary>
        private bool ScanClause(int index, ref int caseDepth, out bool separated)
        {
            var text = _texts[index];
            separated = false;

            if (text == ",")
            {
                separated = true;
                return false;
            }

            if (_tokens[index].Kind == SqlTokenKind.Keyword)
            {
                switch (_upper[index])
                {
                    case "CASE":
                        caseDepth++;
                        return true;
                    // CASE を閉じる END は句の切れ目ではない。
                    case "END" when caseDepth > 0:
                        caseDepth--;
                        return true;
                }
            }

            return text != ";" && !IsClauseStart(index);
        }

        private bool IsClauseStart(int index)
        {
            if (_tokens[index].Kind != SqlTokenKind.Keyword)
            {
                return false;
            }

            var previous = index > 0 ? _upper[index - 1] : string.Empty;

            return _upper[index] switch
            {
                // CASE を閉じる END は式の途中なので折らない。
                "END" => _caseDepth == 0,
                "JOIN" => !JoinModifiers.Contains(previous),
                "FROM" => previous != "DELETE",
                // LEFT(...) / RIGHT(...) は関数、WITH (NOLOCK) はテーブルヒント。
                "LEFT" or "RIGHT" or "WITH" => NextText(index) != "(",
                var word => ClauseStarters.Contains(word)
            };
        }

        /// <summary>WHERE や ON の中の AND / OR。BETWEEN a AND b の AND では折らない。</summary>
        private bool IsBooleanConnector(int index)
        {
            if (_tokens[index].Kind != SqlTokenKind.Keyword)
            {
                return false;
            }

            return _upper[index] switch
            {
                "OR" => true,
                "AND" => !_betweenPending,
                _ => false
            };
        }

        private int IndentFor(int index)
        {
            if (_parenDepth > 0)
            {
                return _parenDepth + 1;
            }

            return IsClauseStart(index) ? 0 : 1;
        }

        private bool NeedsSpace(int index)
        {
            var text = _texts[index];
            var previous = _texts[index - 1];

            if (text is "," or ";" or ")" || previous == "(")
            {
                return false;
            }

            if (text is "." or "::" || previous is "." or "::")
            {
                return false;
            }

            // count(*) や dbo.orders(...) は名前と括弧をくっつける。IN (...) は離す。
            if (text == "(")
            {
                return _tokens[index - 1].Kind
                    is not (SqlTokenKind.Function or SqlTokenKind.Identifier or SqlTokenKind.Variable);
            }

            return true;
        }

        private void Track(int index)
        {
            var token = _tokens[index];
            var text = _texts[index];

            if (text == "(")
            {
                _parenDepth++;
            }
            else if (text == ")")
            {
                _parenDepth = Math.Max(0, _parenDepth - 1);
            }

            if (token.Kind == SqlTokenKind.Keyword)
            {
                TrackKeyword(_upper[index]);
            }

            _pendingBlankLine = text == ";" || IsBatchSeparator(index);
            _forceNewLine = _pendingBlankLine || IsLineComment(token, text);
        }

        private void TrackKeyword(string word)
        {
            switch (word)
            {
                case "CASE":
                    _caseDepth++;
                    break;
                case "END" when _caseDepth > 0:
                    _caseDepth--;
                    break;
                case "BETWEEN":
                    _betweenPending = true;
                    break;
                case "AND":
                    _betweenPending = false;
                    break;
            }
        }

        private void NewLine(int indent)
        {
            _builder.Append('\n');

            if (_pendingBlankLine)
            {
                _builder.Append('\n');
                _pendingBlankLine = false;
            }

            for (var level = 0; level < indent; level++)
            {
                _builder.Append(Indent);
            }
        }

        private bool IsBatchSeparator(int index) =>
            _tokens[index].Kind == SqlTokenKind.Keyword && _upper[index] == "GO";

        private static bool IsLineComment(SqlToken token, string text) =>
            token.Kind == SqlTokenKind.Comment && text.StartsWith("--", StringComparison.Ordinal);

        private string NextText(int index) => index + 1 < _texts.Count ? _texts[index + 1] : string.Empty;
    }
}
