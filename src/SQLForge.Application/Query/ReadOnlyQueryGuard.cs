using System.Text;

namespace SQLForge.Application.Query;

/// <summary>
/// 読み取り専用で開いた接続に、書き込みの文面が来ていないかを見る
/// （<see cref="Domain.Connections.AccessMode.ReadOnly"/> が約束している歯止め）。
///
/// これは事故を止めるためのものであって、権限の代わりではない。動的 SQL のように
/// 文面からは分からない書き込みは通ってしまうので、本当に書かせたくない接続には
/// サーバー側で読み取り専用のロールを与えること。
///
/// 判定はキーワードの有無だけで行う。文面全体を見るのは、先頭の 1 文だけを見ると
/// 「SELECT 1; DELETE FROM t」を通してしまうため。取り違えを避けるため、
/// 文字列リテラル・引用符付き識別子・コメントの中は先に潰してから数える。
/// </summary>
public static class ReadOnlyQueryGuard
{
    /// <summary>書き込み・DDL・権限操作を始めるキーワード。</summary>
    private static readonly HashSet<string> WriteKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE", "UPSERT",
        "CREATE", "ALTER", "DROP", "RENAME",
        "GRANT", "REVOKE", "DENY",
        "EXEC", "EXECUTE", "CALL",
        "BACKUP", "RESTORE", "DBCC", "BULK", "SHUTDOWN", "KILL"
    };

    /// <summary>見つかった書き込みのキーワード。無ければ null。</summary>
    public static string? FindWriteKeyword(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        foreach (var word in Words(Scrub(sql)))
        {
            if (WriteKeywords.Contains(word))
            {
                return word.ToUpperInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// コメント・文字列リテラル・引用符付き識別子を空白へ潰す。
    /// 'delete' や [drop] を書き込みと取り違えないようにするため。
    /// </summary>
    private static string Scrub(string sql)
    {
        var scrubbed = new StringBuilder(sql.Length);

        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            var next = index + 1 < sql.Length ? sql[index + 1] : '\0';

            // 行コメント。改行までを捨てる（改行自体は語の切れ目なので捨ててよい）。
            if (current == '-' && next == '-')
            {
                while (index < sql.Length && sql[index] != '\n')
                {
                    index++;
                }

                scrubbed.Append(' ');
                continue;
            }

            // ブロックコメント。閉じていなければ末尾までを捨てる。
            if (current == '/' && next == '*')
            {
                index += 2;

                while (index + 1 < sql.Length && !(sql[index] == '*' && sql[index + 1] == '/'))
                {
                    index++;
                }

                index++;
                scrubbed.Append(' ');
                continue;
            }

            // 文字列リテラルと引用符付き識別子。'it''s' のような重ね書きは、
            // 2 つめの引用符から新しいリテラルが始まったものとして数えても結果は変わらない。
            if (current is '\'' or '"' or '[')
            {
                var closing = current == '[' ? ']' : current;
                index++;

                while (index < sql.Length && sql[index] != closing)
                {
                    index++;
                }

                scrubbed.Append(' ');
                continue;
            }

            scrubbed.Append(current);
        }

        return scrubbed.ToString();
    }

    /// <summary>
    /// 語へ切り分ける。区切りで切るので、update_count のような識別子が
    /// UPDATE と一致することはない。
    /// </summary>
    private static IEnumerable<string> Words(string text)
    {
        var word = new StringBuilder();

        foreach (var current in text)
        {
            if (char.IsLetterOrDigit(current) || current is '_' or '@' or '#' or '$')
            {
                word.Append(current);
                continue;
            }

            if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            yield return word.ToString();
        }
    }
}
