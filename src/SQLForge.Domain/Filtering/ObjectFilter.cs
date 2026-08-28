namespace SQLForge.Domain.Filtering;

/// <summary>
/// 見出し 1 つに掛かっている絞り込み。SSMS の「フィルターの設定」で入れた行のうち、
/// 値が入っているものだけがここに残る。条件は AND で重なる。
/// </summary>
public sealed class ObjectFilter
{
    /// <summary>絞り込みなし。掛かっていない見出しはこれを持つ。</summary>
    public static ObjectFilter None { get; } = new([]);

    public ObjectFilter(IReadOnlyList<TextFilterClause> texts, DateFilterClause? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(texts);

        Texts = texts;
        CreatedAt = createdAt;
    }

    /// <summary>文字列のプロパティの条件。</summary>
    public IReadOnlyList<TextFilterClause> Texts { get; }

    /// <summary>作成日の条件。入っていなければ null。</summary>
    public DateFilterClause? CreatedAt { get; }

    /// <summary>条件が 1 つも無いか。無ければ見出しの「(フィルター適用)」も出さない。</summary>
    public bool IsEmpty => Texts.Count == 0 && CreatedAt is null;

    /// <summary>すべての条件に当てはまるか。条件が無ければ何でも通す。</summary>
    public bool Matches(ObjectFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Texts.All(clause => clause.Matches(target))
            && (CreatedAt is null || CreatedAt.Matches(target.CreatedAt));
    }
}
