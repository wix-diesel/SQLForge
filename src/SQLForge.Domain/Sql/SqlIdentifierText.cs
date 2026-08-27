namespace SQLForge.Domain.Sql;

/// <summary>
/// エディタの文面に出てくる識別子の引用符を外したり付けたりする。
///
/// 実行する文面を組み立てるための引用符付け（ドライバー側の
/// SqlServerIdentifier など）とは役割が違う。こちらはエディタが読み書きする
/// 文字列の見た目だけを扱う。
/// </summary>
public static class SqlIdentifierText
{
    /// <summary>[名前] "名前" `名前` の引用符を外す。付いていなければそのまま返す。</summary>
    public static string Unquote(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        if (identifier.Length < 2)
        {
            return identifier;
        }

        var (open, close) = (identifier[0], identifier[^1]);

        return (open, close) switch
        {
            ('[', ']') => identifier[1..^1].Replace("]]", "]", StringComparison.Ordinal),
            ('"', '"') => identifier[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal),
            ('`', '`') => identifier[1..^1].Replace("``", "`", StringComparison.Ordinal),
            _ => identifier
        };
    }

    /// <summary>
    /// そのままでは識別子として通らない名前にだけ [ ] を付ける。
    /// 引用符の形は SQL Server のもの（いま実装があるドライバーがこれだけのため）。
    /// </summary>
    public static string QuoteIfNeeded(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return NeedsQuoting(identifier)
            ? $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]"
            : identifier;
    }

    private static bool NeedsQuoting(string identifier)
    {
        if (identifier.Length == 0 || SqlKeywords.IsKeyword(identifier))
        {
            return true;
        }

        if (!char.IsLetter(identifier[0]) && identifier[0] is not ('_' or '#'))
        {
            return true;
        }

        return identifier.Any(character => !char.IsLetterOrDigit(character) && character is not ('_' or '#' or '$'));
    }
}
