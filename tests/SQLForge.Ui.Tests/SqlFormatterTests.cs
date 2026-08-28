using SQLForge.Domain.Sql;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 整形。「見た目を整えるだけで、実行の結果は変えない」ことを機械的に押さえる。
/// </summary>
public class SqlFormatterTests
{
    /// <summary>整形にかける見本。べき等と字句の不変はすべての見本で確かめる。</summary>
    public static TheoryData<string> Samples =>
    [
        "select a, b from dbo.orders where a = 1",
        "SELECT   top(10)*from t",
        "select 'from where' -- select\nfrom t",
        "select /* 途中 */ id from t where id in (1,2,3)",
        "select a from t inner join u on t.id=u.id left outer join v on v.id=u.id",
        "select case when a then 1 else 2 end as x, b from t",
        "select 1; select 2",
        "select 1\ngo\nselect 2",
        "update t set a=1, b=2 where id between 1 and 5 or id is null",
        "insert into dbo.t (a, b) values (1, 'x')",
        "select left(name,3), [注文 一覧].id from [注文 一覧] with (nolock)",
        "",
        "   ",
        "-- 覚え書きだけ"
    ];

    [Fact]
    public void 句ごとに改行して予約語を大文字にする()
    {
        var formatted = SqlFormatter.Format("select a, b from dbo.orders where a = 1");

        Assert.Equal(
            "SELECT\n    a,\n    b\nFROM dbo.orders\nWHERE a = 1",
            formatted);
    }

    [Fact]
    public void 列が一つだけなら一行に収める()
    {
        var formatted = SqlFormatter.Format("select top (1000) * from [dbo].[orders];");

        Assert.Equal("SELECT TOP (1000) *\nFROM [dbo].[orders];", formatted);
    }

    [Fact]
    public void 括弧の中は折らない()
    {
        var formatted = SqlFormatter.Format("select id from t where id in (1,2,3)");

        Assert.Equal("SELECT id\nFROM t\nWHERE id IN (1, 2, 3)", formatted);
    }

    [Fact]
    public void 結合の修飾語は一行にまとめる()
    {
        var formatted = SqlFormatter.Format("select a from t inner join u on t.id=u.id");

        Assert.Equal("SELECT a\nFROM t\nINNER JOIN u ON t.id = u.id", formatted);
    }

    [Fact]
    public void 行コメントの後ろは必ず改行する()
    {
        var formatted = SqlFormatter.Format("select 1 -- 覚え書き\nfrom t");

        Assert.Equal("SELECT 1 -- 覚え書き\nFROM t", formatted);
    }

    [Fact]
    public void バッチの区切りは一行に独立させる()
    {
        var formatted = SqlFormatter.Format("select 1\ngo\nselect 2");

        Assert.Equal("SELECT 1\nGO\n\nSELECT 2", formatted);
    }

    [Fact]
    public void 文の切れ目には空行を入れる()
    {
        var formatted = SqlFormatter.Format("select 1; select 2");

        Assert.Equal("SELECT 1;\n\nSELECT 2", formatted);
    }

    [Fact]
    public void 文字列とコメントの中身は書き換えない()
    {
        var formatted = SqlFormatter.Format("select 'from where' -- select\nfrom t");

        Assert.Contains("'from where'", formatted, StringComparison.Ordinal);
        Assert.Contains("-- select", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void 別名を大文字にはしない()
    {
        var formatted = SqlFormatter.Format("select o.id from dbo.orders as o");

        Assert.Contains("dbo.orders AS o", formatted, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void 二回かけても変わらない(string sql)
    {
        var once = SqlFormatter.Format(sql);

        Assert.Equal(once, SqlFormatter.Format(once));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void 空白と予約語の大小のほかは字句の並びを変えない(string sql)
    {
        var formatted = SqlFormatter.Format(sql);

        Assert.Equal(Significant(sql), Significant(formatted));
    }

    /// <summary>空白を除いた字句の並び。予約語の大文字化を無視するために大文字へ揃える。</summary>
    private static IReadOnlyList<(SqlTokenKind Kind, string Text)> Significant(string sql) =>
        SqlLexer.Tokenize(sql)
            .Where(token => token.Kind != SqlTokenKind.Whitespace)
            .Select(token => (token.Kind, Text: token.TextOf(sql).ToUpperInvariant()))
            .ToList();
}
