namespace SQLForge.Domain.Filtering;

/// <summary>
/// 文字列のプロパティに使う演算子。SSMS の「フィルターの設定」で名前の行に並ぶものと同じ。
/// </summary>
public enum TextFilterOperator
{
    /// <summary>値を含む。</summary>
    Contains,

    /// <summary>値を含まない。</summary>
    NotContains,

    /// <summary>値と等しい。</summary>
    Equal,

    /// <summary>値と等しくない。</summary>
    NotEqual
}

/// <summary>演算子ごとの表示名。</summary>
public static class TextFilterOperators
{
    /// <summary>ダイアログに並べる順。SSMS と同じ並びにする。</summary>
    public static IReadOnlyList<TextFilterOperator> All { get; } =
    [
        TextFilterOperator.Contains,
        TextFilterOperator.NotContains,
        TextFilterOperator.Equal,
        TextFilterOperator.NotEqual
    ];

    public static string DisplayName(this TextFilterOperator @operator) => @operator switch
    {
        TextFilterOperator.Contains => "次を含む",
        TextFilterOperator.NotContains => "次を含まない",
        TextFilterOperator.Equal => "次と等しい",
        TextFilterOperator.NotEqual => "次と等しくない",
        _ => "不明な演算子"
    };
}
