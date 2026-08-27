namespace SQLForge.Domain.Sql;

/// <summary>
/// SQL の字句 1 つの種類。色分け・補完・整形の 3 つが同じ種類を見る。
///
/// 色分けの都合で決めた区分なので、<see cref="Function"/> と <see cref="Type"/> は
/// 語彙表に載っているかどうかだけで決まる（構文の位置は見ない）。
/// Themes/Tokens.axaml の Syntax*Brush と 1 対 1 で対応する。
/// </summary>
public enum SqlTokenKind
{
    /// <summary>空白・改行。</summary>
    Whitespace,

    /// <summary>-- 行末まで、または /* ... */。</summary>
    Comment,

    /// <summary>'文字列' と N'文字列'。</summary>
    String,

    /// <summary>数値リテラル（0x 始まりの 16 進を含む）。</summary>
    Number,

    /// <summary>予約語。</summary>
    Keyword,

    /// <summary>組み込み関数の名前。</summary>
    Function,

    /// <summary>組み込みの型名。</summary>
    Type,

    /// <summary>識別子。[名前] "名前" `名前` の引用符付きも含む。</summary>
    Identifier,

    /// <summary>@変数 と @@システム変数。</summary>
    Variable,

    /// <summary>区切り記号と演算子。</summary>
    Punctuation
}
