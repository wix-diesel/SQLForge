namespace SQLForge.Application.Query;

/// <summary>
/// 補完の結果。差し込むときに置き換える範囲も一緒に返す
/// （打ちかけの語をそのまま残すと「ordord」になるため）。
/// </summary>
/// <param name="ReplaceOffset">置き換える範囲の先頭。</param>
/// <param name="ReplaceLength">置き換える文字数。</param>
/// <param name="Items">候補。並び順のまま出す。</param>
public sealed record SqlCompletionResult(
    int ReplaceOffset,
    int ReplaceLength,
    IReadOnlyList<SqlCompletionItem> Items)
{
    /// <summary>候補なし。</summary>
    public static SqlCompletionResult Empty { get; } = new(0, 0, []);

    public bool IsEmpty => Items.Count == 0;
}
