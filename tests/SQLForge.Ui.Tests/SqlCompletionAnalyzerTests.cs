using SQLForge.Domain.Sql;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 補完の文脈判定。カタログには触らないので、接続なしで確かめられる。
/// </summary>
public class SqlCompletionAnalyzerTests
{
    [Fact]
    public void FROMの後ろではテーブルを出す()
    {
        const string sql = "SELECT * FROM ";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        Assert.Equal(SqlCompletionKind.Table, context.Kind);
        Assert.Equal(string.Empty, context.Prefix);
        Assert.Null(context.Qualifier);
    }

    [Fact]
    public void SELECTの後ろでは列を出す()
    {
        const string sql = "SELECT  FROM dbo.orders";

        var context = SqlCompletionAnalyzer.Analyze(sql, "SELECT ".Length);

        Assert.Equal(SqlCompletionKind.Column, context.Kind);
    }

    [Fact]
    public void 打ちかけの語と置き換える範囲を返す()
    {
        const string sql = "SELECT ord";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        Assert.Equal("ord", context.Prefix);
        Assert.Equal(7, context.ReplaceOffset);
        Assert.Equal(3, context.ReplaceLength);
    }

    [Fact]
    public void 別名の後ろの点ではそのテーブルの列を出す()
    {
        const string sql = "SELECT o. FROM dbo.orders AS o";

        var context = SqlCompletionAnalyzer.Analyze(sql, "SELECT o.".Length);

        Assert.Equal(SqlCompletionKind.Column, context.Kind);
        Assert.Equal("o", context.Qualifier);

        var table = Assert.Single(context.Tables);
        Assert.Equal(new SqlTableReference("dbo", "orders", "o"), table);
    }

    [Fact]
    public void 別名を書かないテーブルはテーブル名で受ける()
    {
        const string sql = "SELECT orders. FROM dbo.orders";

        var context = SqlCompletionAnalyzer.Analyze(sql, "SELECT orders.".Length);

        Assert.Equal("orders", context.Qualifier);
        Assert.True(context.Tables[0].Matches("orders"));
    }

    [Fact]
    public void 結合した二つのテーブルをどちらも覚える()
    {
        const string sql = "SELECT * FROM dbo.orders o INNER JOIN dbo.customers AS c ON o.id = c.id WHERE ";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        Assert.Equal(SqlCompletionKind.Column, context.Kind);
        Assert.Equal(
            [new SqlTableReference("dbo", "orders", "o"), new SqlTableReference("dbo", "customers", "c")],
            context.Tables);
    }

    [Fact]
    public void カンマで並べたテーブルも覚える()
    {
        const string sql = "SELECT * FROM dbo.orders o, dbo.customers c WHERE ";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        Assert.Equal(2, context.Tables.Count);
    }

    [Fact]
    public void 前の文のテーブルは混ぜない()
    {
        const string sql = "SELECT * FROM dbo.orders; SELECT * FROM dbo.customers WHERE ";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        var table = Assert.Single(context.Tables);
        Assert.Equal("customers", table.Name);
    }

    [Fact]
    public void 文字列の中では補完しない()
    {
        const string sql = "SELECT * FROM t WHERE name = 'ord";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        Assert.Equal(SqlCompletionKind.None, context.Kind);
    }

    [Fact]
    public void コメントの中では補完しない()
    {
        const string sql = "-- select fr";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        Assert.Equal(SqlCompletionKind.None, context.Kind);
    }

    [Fact]
    public void 何も書いていないときは予約語を出す()
    {
        var context = SqlCompletionAnalyzer.Analyze("sel", 3);

        Assert.Equal(SqlCompletionKind.Keyword, context.Kind);
        Assert.Equal("sel", context.Prefix);
    }

    [Fact]
    public void 引用符を開いた途中でも中身で絞り込む()
    {
        const string sql = "SELECT * FROM [注";

        var context = SqlCompletionAnalyzer.Analyze(sql, sql.Length);

        Assert.Equal(SqlCompletionKind.Table, context.Kind);
        Assert.Equal("注", context.Prefix);
        Assert.Equal("SELECT * FROM ".Length, context.ReplaceOffset);
    }
}
