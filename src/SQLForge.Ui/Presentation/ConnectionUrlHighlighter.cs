namespace SQLForge.Ui.Presentation;

/// <summary>
/// 接続 URL を色分け用の断片に切る。表示だけの処理なので UI 層に置く。
/// </summary>
public static class ConnectionUrlHighlighter
{
    public static IReadOnlyList<ConnectionUrlPart> Split(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return [];
        }

        var parts = new List<ConnectionUrlPart>();
        var rest = TakeScheme(url, parts);
        var queryIndex = rest.IndexOf('?');

        if (queryIndex < 0)
        {
            AddPath(rest, parts);
            return parts;
        }

        AddPath(rest[..queryIndex], parts);
        parts.Add(Plain("?"));
        AddQuery(rest[(queryIndex + 1)..], parts);

        return parts;
    }

    private static string TakeScheme(string url, ICollection<ConnectionUrlPart> parts)
    {
        var separator = url.Contains("://", StringComparison.Ordinal) ? "://" : ":";
        var index = url.IndexOf(separator, StringComparison.Ordinal);
        if (index < 0)
        {
            return url;
        }

        var end = index + separator.Length;
        parts.Add(new ConnectionUrlPart(url[..end], ConnectionUrlPartKind.Scheme));
        return url[end..];
    }

    private static void AddPath(string path, ICollection<ConnectionUrlPart> parts)
    {
        var index = path.LastIndexOf('/');
        if (index < 0 || index == path.Length - 1)
        {
            parts.Add(Plain(path));
            return;
        }

        parts.Add(Plain(path[..(index + 1)]));
        parts.Add(new ConnectionUrlPart(path[(index + 1)..], ConnectionUrlPartKind.Database));
    }

    private static void AddQuery(string query, ICollection<ConnectionUrlPart> parts)
    {
        var first = true;
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!first)
            {
                parts.Add(Plain("&"));
            }

            first = false;
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                parts.Add(Plain(pair));
                continue;
            }

            parts.Add(new ConnectionUrlPart(pair[..separator], ConnectionUrlPartKind.ParameterName));
            parts.Add(Plain(pair[separator..]));
        }
    }

    private static ConnectionUrlPart Plain(string text) => new(text, ConnectionUrlPartKind.Plain);
}
