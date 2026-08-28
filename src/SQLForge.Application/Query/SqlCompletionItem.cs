namespace SQLForge.Application.Query;

/// <summary>補完の候補 1 つの出どころ。ポップアップのバッジに使う。</summary>
public enum SqlCompletionItemKind
{
    Column,
    Table,
    Schema,
    Function,
    Keyword
}

/// <summary>
/// 補完の候補 1 つ。
/// </summary>
/// <param name="Label">一覧に出す文字列。絞り込みもこれで行う。</param>
/// <param name="InsertText">実際に差し込む文字列。要るときだけ引用符が付く。</param>
/// <param name="Kind">出どころ。</param>
/// <param name="Detail">右側に薄く出す補足（列の型やスキーマ名）。</param>
public sealed record SqlCompletionItem(
    string Label,
    string InsertText,
    SqlCompletionItemKind Kind,
    string? Detail = null);
