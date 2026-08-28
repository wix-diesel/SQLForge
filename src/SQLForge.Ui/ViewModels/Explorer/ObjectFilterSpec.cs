using SQLForge.Domain.Filtering;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 見出し 1 つぶんの絞り込みの支度。何で絞り込めるか・どこへ設定を出しに行くかを束ねる。
/// </summary>
/// <param name="Properties">条件にできるプロパティ。空なら絞り込みのメニューを出さない。</param>
/// <param name="Editor">「フィルターの設定」の行き先。無ければメニューを出さない。</param>
/// <param name="Scope">
/// ダイアログに出す居場所（例: sales_db/dbo）。無ければ見出しの名前だけを出す。
/// </param>
public sealed record ObjectFilterSpec(
    IReadOnlyList<ObjectFilterProperty> Properties,
    IObjectFilterEditor? Editor,
    string? Scope = null)
{
    /// <summary>ダイアログの見出しに出す道のり。</summary>
    public string Describe(string title) => Scope is { Length: > 0 } scope ? $"{scope}/{title}" : title;
}
