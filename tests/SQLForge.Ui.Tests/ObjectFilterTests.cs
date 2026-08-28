using SQLForge.Domain.Filtering;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ツリーの絞り込みの規則。SSMS のフィルターと同じで、条件は AND で重なり、
/// 文字列は大文字と小文字を区別せず、値が空の行は条件にならない。
/// </summary>
public class ObjectFilterTests
{
    private static readonly ObjectFilterTarget Orders = new("orders", new DateTime(2026, 4, 15, 10, 30, 0));

    [Fact]
    public void 条件が無ければ何でも通す()
    {
        Assert.True(ObjectFilter.None.IsEmpty);
        Assert.True(ObjectFilter.None.Matches(Orders));
    }

    [Theory]
    [InlineData(TextFilterOperator.Contains, "ORD", true)]
    [InlineData(TextFilterOperator.Contains, "xyz", false)]
    [InlineData(TextFilterOperator.NotContains, "ORD", false)]
    [InlineData(TextFilterOperator.NotContains, "xyz", true)]
    [InlineData(TextFilterOperator.Equal, "Orders", true)]
    [InlineData(TextFilterOperator.Equal, "order", false)]
    [InlineData(TextFilterOperator.NotEqual, "Orders", false)]
    [InlineData(TextFilterOperator.NotEqual, "order", true)]
    public void 名前の条件は大文字と小文字を区別しない(TextFilterOperator @operator, string value, bool expected)
    {
        var clause = new TextFilterClause(ObjectFilterProperty.Name, @operator, value);

        Assert.Equal(expected, clause.Matches(Orders));
    }

    [Fact]
    public void 値が空の条件は作れない()
    {
        // 空の行は「条件なし」として読み飛ばす決まりなので、そもそも条件にしない。
        Assert.Throws<ArgumentException>(() =>
            new TextFilterClause(ObjectFilterProperty.Name, TextFilterOperator.Contains, "   "));
    }

    [Fact]
    public void 条件はANDで重なる()
    {
        var filter = new ObjectFilter(
        [
            new TextFilterClause(ObjectFilterProperty.Name, TextFilterOperator.Contains, "or"),
            new TextFilterClause(ObjectFilterProperty.Name, TextFilterOperator.NotContains, "old")
        ]);

        Assert.False(filter.IsEmpty);
        Assert.True(filter.Matches(Orders));
        Assert.False(filter.Matches(new ObjectFilterTarget("old_orders")));
    }

    [Fact]
    public void 作成日は日の単位で比べる()
    {
        var clause = new DateFilterClause(DateFilterOperator.Equal, new DateOnly(2026, 4, 15));

        // 時刻まで持っていても、比べるのは日まで。
        Assert.True(clause.Matches(new DateTime(2026, 4, 15, 23, 59, 0)));
        Assert.False(clause.Matches(new DateTime(2026, 4, 16)));
    }

    [Theory]
    [InlineData(DateFilterOperator.LessThan, 15, false)]
    [InlineData(DateFilterOperator.LessThanOrEqual, 15, true)]
    [InlineData(DateFilterOperator.GreaterThan, 15, false)]
    [InlineData(DateFilterOperator.GreaterThanOrEqual, 15, true)]
    [InlineData(DateFilterOperator.GreaterThan, 1, true)]
    [InlineData(DateFilterOperator.NotEqual, 15, false)]
    public void 作成日の大小を比べる(DateFilterOperator @operator, int day, bool expected)
    {
        var clause = new DateFilterClause(@operator, new DateOnly(2026, 4, day));

        Assert.Equal(expected, clause.Matches(new DateTime(2026, 4, 15)));
    }

    [Fact]
    public void 次の間は両端を含む()
    {
        var clause = new DateFilterClause(
            DateFilterOperator.Between, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        Assert.True(clause.Matches(new DateTime(2026, 4, 1)));
        Assert.True(clause.Matches(new DateTime(2026, 4, 30)));
        Assert.False(clause.Matches(new DateTime(2026, 5, 1)));

        var outside = new DateFilterClause(
            DateFilterOperator.NotBetween, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        Assert.False(outside.Matches(new DateTime(2026, 4, 15)));
        Assert.True(outside.Matches(new DateTime(2026, 5, 1)));
    }

    [Fact]
    public void 次の間には終わりの日が要る()
    {
        Assert.Throws<ArgumentException>(() =>
            new DateFilterClause(DateFilterOperator.Between, new DateOnly(2026, 4, 1)));
    }

    [Fact]
    public void 作成日を読めないものは日付の条件に通さない()
    {
        // 「いつ作られたか分からないもの」を通すと、条件と食い違う行が並んでしまう。
        var filter = new ObjectFilter(
            [], new DateFilterClause(DateFilterOperator.GreaterThan, new DateOnly(2000, 1, 1)));

        Assert.False(filter.Matches(new ObjectFilterTarget("orders")));
        Assert.True(filter.Matches(Orders));
    }
}
