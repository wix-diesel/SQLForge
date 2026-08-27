using SQLForge.Domain.Sql;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 字句解析。色分け・補完・整形はすべてこの結果を見るので、ここが崩れると 3 つとも崩れる。
/// </summary>
public class SqlLexerTests
{
    [Fact]
    public void 予約語と識別子と文字列と数値を切り分ける()
    {
        const string sql = "SELECT 1, 'x' FROM dbo.orders";

        Assert.Equal(
            [
                SqlTokenKind.Keyword, SqlTokenKind.Whitespace, SqlTokenKind.Number, SqlTokenKind.Punctuation,
                SqlTokenKind.Whitespace, SqlTokenKind.String, SqlTokenKind.Whitespace, SqlTokenKind.Keyword,
                SqlTokenKind.Whitespace, SqlTokenKind.Identifier, SqlTokenKind.Punctuation, SqlTokenKind.Identifier
            ],
            SqlLexer.Tokenize(sql).Select(token => token.Kind));
    }

    [Fact]
    public void 字句は文面を隙間なく覆う()
    {
        const string sql = "SELECT [a b], N'ゆ''き' -- 覚え書き\nFROM t /* 途中 */ WHERE x <= @p";

        var tokens = SqlLexer.Tokenize(sql);

        Assert.Equal(sql, string.Concat(tokens.Select(token => token.TextOf(sql))));
        Assert.Equal(0, tokens[0].Offset);
    }

    [Fact]
    public void 文字列の中のハイフン二つはコメントにならない()
    {
        const string sql = "SELECT '-- これは文字列'";

        var tokens = SqlLexer.Tokenize(sql).Where(token => !token.IsTrivia).ToList();

        Assert.Equal(SqlTokenKind.String, tokens[1].Kind);
        Assert.Equal("'-- これは文字列'", tokens[1].TextOf(sql));
    }

    [Fact]
    public void 文字列の中の引用符二つは閉じ記号として扱わない()
    {
        const string sql = "'it''s'";

        var tokens = SqlLexer.Tokenize(sql);

        Assert.Single(tokens);
        Assert.Equal(SqlTokenKind.String, tokens[0].Kind);
    }

    [Fact]
    public void 閉じていない文字列は末尾までを一つの字句にする()
    {
        const string sql = "SELECT 'まだ書きかけ";

        var tokens = SqlLexer.Tokenize(sql);

        Assert.Equal(SqlTokenKind.String, tokens[^1].Kind);
        Assert.Equal(sql.Length, tokens[^1].End);
    }

    [Fact]
    public void ブロックコメントは入れ子にできる()
    {
        const string sql = "/* 外 /* 内 */ まだ外 */ SELECT";

        var tokens = SqlLexer.Tokenize(sql);

        Assert.Equal(SqlTokenKind.Comment, tokens[0].Kind);
        Assert.Equal("/* 外 /* 内 */ まだ外 */", tokens[0].TextOf(sql));
    }

    [Fact]
    public void 角かっこの識別子は中の空白ごと一つにする()
    {
        const string sql = "[注文 一覧]";

        var tokens = SqlLexer.Tokenize(sql);

        Assert.Single(tokens);
        Assert.Equal(SqlTokenKind.Identifier, tokens[0].Kind);
    }

    [Theory]
    [InlineData("<=")]
    [InlineData(">=")]
    [InlineData("<>")]
    [InlineData("!=")]
    [InlineData("::")]
    public void 二文字の演算子は割らない(string oper)
    {
        var tokens = SqlLexer.Tokenize($"a{oper}b").Where(token => !token.IsTrivia).ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal(oper, tokens[1].TextOf($"a{oper}b"));
    }

    [Fact]
    public void 変数と組み込み関数と型名を見分ける()
    {
        const string sql = "DECLARE @count INT = COUNT(@@ROWCOUNT)";

        var tokens = SqlLexer.Tokenize(sql).Where(token => !token.IsTrivia).ToList();

        Assert.Equal(SqlTokenKind.Variable, tokens[1].Kind);
        Assert.Equal(SqlTokenKind.Type, tokens[2].Kind);
        Assert.Equal(SqlTokenKind.Function, tokens[4].Kind);
        Assert.Equal(SqlTokenKind.Variable, tokens[6].Kind);
    }

    [Fact]
    public void 小数と十六進を一つの数値として読む()
    {
        const string sql = "1.5 0x1F 1e3";

        var numbers = SqlLexer.Tokenize(sql)
            .Where(token => token.Kind == SqlTokenKind.Number)
            .Select(token => token.TextOf(sql));

        Assert.Equal(["1.5", "0x1F", "1e3"], numbers);
    }
}
