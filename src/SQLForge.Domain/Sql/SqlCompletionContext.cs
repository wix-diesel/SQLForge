namespace SQLForge.Domain.Sql;

/// <summary>
/// 補完のためにキャレットの周りを読んだ結果。カタログには一切触らないので、
/// 接続なしで組み立てられる（候補の中身を決めるのは Application 側）。
/// </summary>
/// <param name="Kind">何を出すか。</param>
/// <param name="Prefix">打ちかけの文字列。候補の絞り込みに使う。</param>
/// <param name="ReplaceOffset">候補を差し込むときに置き換える範囲の先頭。</param>
/// <param name="ReplaceLength">置き換える文字数。</param>
/// <param name="Qualifier">直前の . の左側（別名・テーブル名・スキーマ名のどれか）。</param>
/// <param name="Tables">今の文が読んでいるテーブル。</param>
public sealed record SqlCompletionContext(
    SqlCompletionKind Kind,
    string Prefix,
    int ReplaceOffset,
    int ReplaceLength,
    string? Qualifier,
    IReadOnlyList<SqlTableReference> Tables)
{
    /// <summary>補完しない位置。</summary>
    public static SqlCompletionContext None { get; } =
        new(SqlCompletionKind.None, string.Empty, 0, 0, null, []);
}
