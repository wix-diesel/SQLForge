namespace SQLForge.Domain.Filtering;

/// <summary>
/// 文字列のプロパティ 1 つぶんの条件。SSMS と同じく、値が空の行は条件にならないので、
/// 空の値でこの型を作ることはできない（作る前に落とす）。
/// </summary>
public sealed record TextFilterClause
{
    public TextFilterClause(ObjectFilterProperty property, TextFilterOperator @operator, string value)
    {
        // 日付のプロパティを渡されても名前で当ててしまわないよう、ここで弾く（日付は DateFilterClause の受け持ち）。
        if (property.IsDate())
        {
            throw new ArgumentException(
                $"{property.DisplayName()}は日付なので、文字列の条件にはできません。", nameof(property));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("絞り込みの値は空にできません。", nameof(value));
        }

        Property = property;
        Operator = @operator;
        Value = value;
    }

    public ObjectFilterProperty Property { get; }

    public TextFilterOperator Operator { get; }

    public string Value { get; }

    /// <summary>この条件に当てはまるか。SSMS と同じく大文字と小文字は区別しない。</summary>
    public bool Matches(ObjectFilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        // 文字列で持っているプロパティは今のところ名前だけ（コンストラクタで保証している）。
        var actual = target.Name;

        return Operator switch
        {
            TextFilterOperator.Contains => actual.Contains(Value, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.NotContains => !actual.Contains(Value, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.Equal => string.Equals(actual, Value, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.NotEqual => !string.Equals(actual, Value, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}
