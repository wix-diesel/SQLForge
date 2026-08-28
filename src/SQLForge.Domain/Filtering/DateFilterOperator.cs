namespace SQLForge.Domain.Filtering;

/// <summary>
/// 日付のプロパティに使う演算子。SSMS の「フィルターの設定」で作成日の行に並ぶものと同じ。
/// </summary>
public enum DateFilterOperator
{
    /// <summary>その日。</summary>
    Equal,

    /// <summary>その日ではない。</summary>
    NotEqual,

    /// <summary>その日より前。</summary>
    LessThan,

    /// <summary>その日以前。</summary>
    LessThanOrEqual,

    /// <summary>その日より後。</summary>
    GreaterThan,

    /// <summary>その日以後。</summary>
    GreaterThanOrEqual,

    /// <summary>2 つの日の間（両端を含む）。</summary>
    Between,

    /// <summary>2 つの日の間の外。</summary>
    NotBetween
}

/// <summary>演算子ごとの表示名と、終わりの日が要るかどうか。</summary>
public static class DateFilterOperators
{
    /// <summary>ダイアログに並べる順。SSMS と同じ並びにする。</summary>
    public static IReadOnlyList<DateFilterOperator> All { get; } =
    [
        DateFilterOperator.Equal,
        DateFilterOperator.NotEqual,
        DateFilterOperator.LessThan,
        DateFilterOperator.LessThanOrEqual,
        DateFilterOperator.GreaterThan,
        DateFilterOperator.GreaterThanOrEqual,
        DateFilterOperator.Between,
        DateFilterOperator.NotBetween
    ];

    public static string DisplayName(this DateFilterOperator @operator) => @operator switch
    {
        DateFilterOperator.Equal => "次と等しい",
        DateFilterOperator.NotEqual => "次と等しくない",
        DateFilterOperator.LessThan => "次より小さい",
        DateFilterOperator.LessThanOrEqual => "次以下",
        DateFilterOperator.GreaterThan => "次より大きい",
        DateFilterOperator.GreaterThanOrEqual => "次以上",
        DateFilterOperator.Between => "次の間",
        DateFilterOperator.NotBetween => "次の間以外",
        _ => "不明な演算子"
    };

    /// <summary>終わりの日（2 つめの入力欄）が要る演算子か。</summary>
    public static bool NeedsBound(this DateFilterOperator @operator) =>
        @operator is DateFilterOperator.Between or DateFilterOperator.NotBetween;
}
