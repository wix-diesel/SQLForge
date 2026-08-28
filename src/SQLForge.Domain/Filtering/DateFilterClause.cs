namespace SQLForge.Domain.Filtering;

/// <summary>
/// 日付のプロパティ 1 つぶんの条件。「次の間」「次の間以外」だけは終わりの日も要る。
/// 比較は日の単位で行う（作成日は時刻まで持つが、条件に入れられるのは日までのため）。
/// </summary>
public sealed record DateFilterClause
{
    public DateFilterClause(DateFilterOperator @operator, DateOnly value, DateOnly? bound = null)
    {
        if (@operator.NeedsBound() && bound is null)
        {
            throw new ArgumentException("「次の間」の条件には終わりの日が要ります。", nameof(bound));
        }

        Operator = @operator;
        Value = value;
        Bound = bound;
    }

    public DateFilterOperator Operator { get; }

    /// <summary>比べる日。「次の間」では始まりの日。</summary>
    public DateOnly Value { get; }

    /// <summary>「次の間」「次の間以外」の終わりの日。ほかの演算子では null。</summary>
    public DateOnly? Bound { get; }

    /// <summary>
    /// この条件に当てはまるか。作成日を読めなかったもの（null）は当てはまらない扱いにする。
    /// 「いつ作られたか分からないもの」を日付の条件に通すと、条件と食い違う行が並んでしまうため。
    /// </summary>
    public bool Matches(DateTime? createdAt)
    {
        if (createdAt is not { } value)
        {
            return false;
        }

        var day = DateOnly.FromDateTime(value);

        // 両端は「次の間」に含める（SSMS と同じ）。始まりと終わりが逆に入っていても同じに扱う。
        var first = Value;
        var last = Bound ?? Value;

        if (last < first)
        {
            (first, last) = (last, first);
        }

        return Operator switch
        {
            DateFilterOperator.Equal => day == Value,
            DateFilterOperator.NotEqual => day != Value,
            DateFilterOperator.LessThan => day < Value,
            DateFilterOperator.LessThanOrEqual => day <= Value,
            DateFilterOperator.GreaterThan => day > Value,
            DateFilterOperator.GreaterThanOrEqual => day >= Value,
            DateFilterOperator.Between => day >= first && day <= last,
            DateFilterOperator.NotBetween => day < first || day > last,
            _ => true
        };
    }
}
