using System.Globalization;
using System.Text;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// TOML のうち、同じ名前のテーブル配列（<c>[[connection]]</c>）だけを読み書きする最小の実装。
///
/// 保存済み接続のファイルは書くのも読むのもこのアプリなので、扱う形は
/// 「文字列・整数・真偽値の平らなキー」だけに絞ってある。
/// それ以上の記法（入れ子・日時・複数行文字列）が要るようになった時点で、
/// TOML のライブラリへ差し替える。
/// </summary>
internal static class TomlArrayOfTables
{
    /// <summary>テーブル配列を 1 つのテキストに書き出す。値の型は元の型のまま書く。</summary>
    public static string Write(string tableName, IEnumerable<IReadOnlyList<KeyValuePair<string, object>>> tables, string header)
    {
        var text = new StringBuilder();
        text.Append(header);

        foreach (var table in tables)
        {
            text.Append('[').Append('[').Append(tableName).Append(']').Append(']').Append('\n');

            foreach (var (key, value) in table)
            {
                text.Append(key).Append(" = ").Append(Format(value)).Append('\n');
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// テキストからテーブル配列を読む。値は文字列のまま返し、型の解釈は呼び出し側に任せる
    /// （欠けているキーの言い方を、読み手の言葉でそろえたいため）。
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(string tableName, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tables = new List<Dictionary<string, string>>();
        var lineNumber = 0;

        foreach (var raw in text.Split('\n'))
        {
            lineNumber++;
            var line = Strip(raw);

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                RequireHeader(tableName, line, lineNumber);
                tables.Add(new Dictionary<string, string>(StringComparer.Ordinal));
                continue;
            }

            if (tables.Count == 0)
            {
                throw Malformed(lineNumber, $"[[{tableName}]] より前に値があります。");
            }

            var (key, value) = ReadPair(line, lineNumber);
            tables[^1][key] = value;
        }

        return tables;
    }

    private static void RequireHeader(string tableName, string line, int lineNumber)
    {
        if (!string.Equals(line, $"[[{tableName}]]", StringComparison.Ordinal))
        {
            throw Malformed(lineNumber, $"知らない見出しです（扱えるのは [[{tableName}]] だけ）: {line}");
        }
    }

    private static (string Key, string Value) ReadPair(string line, int lineNumber)
    {
        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            throw Malformed(lineNumber, $"「キー = 値」の形になっていません: {line}");
        }

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();

        return (key, value.StartsWith('"') ? Unquote(value, lineNumber) : value);
    }

    /// <summary>行頭・行末の空白と、引用符の外にある注釈を落とす。</summary>
    private static string Strip(string line)
    {
        var inString = false;

        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];

            if (inString && current == '\\')
            {
                index++;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (current == '#' && !inString)
            {
                return line[..index].Trim();
            }
        }

        return line.Trim();
    }

    private static string Format(object value) => value switch
    {
        string text => Quote(text),
        bool flag => flag ? "true" : "false",
        int number => number.ToString(CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException($"TOML に書けない型です: {value?.GetType().Name}")
    };

    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");

        foreach (var character in value)
        {
            text.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => character.ToString()
            });
        }

        return text.Append('"').ToString();
    }

    private static string Unquote(string value, int lineNumber)
    {
        if (value.Length < 2 || !value.EndsWith('"'))
        {
            throw Malformed(lineNumber, $"文字列の引用符が閉じていません: {value}");
        }

        var text = new StringBuilder();

        for (var index = 1; index < value.Length - 1; index++)
        {
            var current = value[index];
            if (current != '\\')
            {
                text.Append(current);
                continue;
            }

            index++;
            if (index >= value.Length - 1)
            {
                throw Malformed(lineNumber, $"エスケープが途中で終わっています: {value}");
            }

            text.Append(value[index] switch
            {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                var other => throw Malformed(lineNumber, $"扱えないエスケープです: \\{other}")
            });
        }

        return text.ToString();
    }

    private static FormatException Malformed(int lineNumber, string reason) =>
        new($"{lineNumber} 行目: {reason}");
}
