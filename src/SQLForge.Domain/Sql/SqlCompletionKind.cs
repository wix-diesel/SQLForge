namespace SQLForge.Domain.Sql;

/// <summary>キャレットの位置で何を出すべきか。</summary>
public enum SqlCompletionKind
{
    /// <summary>出さない（文字列やコメントの中など）。</summary>
    None,

    /// <summary>予約語と組み込み関数。</summary>
    Keyword,

    /// <summary>スキーマとテーブル。FROM や JOIN の後ろ。</summary>
    Table,

    /// <summary>列。SELECT・WHERE・ON の中や、別名に続く . の後ろ。</summary>
    Column
}
