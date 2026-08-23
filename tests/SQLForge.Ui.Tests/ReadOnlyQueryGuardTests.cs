using SQLForge.Application.Query;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 読み取り専用で開いた接続の歯止め。
/// 「通してよいものを止めない」ほうが、見落としより先に苦情になるので両向きを押さえる。
/// </summary>
public class ReadOnlyQueryGuardTests
{
    [Theory]
    [InlineData("SELECT * FROM sales.orders")]
    [InlineData("select top (10) * from dbo.orders order by order_date desc")]
    [InlineData("WITH monthly AS (SELECT 1 AS n) SELECT * FROM monthly")]
    [InlineData("SELECT create_date, modify_date FROM sys.tables")]
    [InlineData("SELECT 'delete from orders' AS sample")]
    [InlineData("-- DROP TABLE orders\nSELECT 1")]
    [InlineData("/* update everything */ SELECT 1")]
    [InlineData("SELECT [drop] FROM dbo.reserved_words")]
    public void 読み取りだけの文は通る(string sql) =>
        Assert.Null(ReadOnlyQueryGuard.FindWriteKeyword(sql));

    [Theory]
    [InlineData("DELETE FROM sales.orders", "DELETE")]
    [InlineData("update dbo.orders set status = 'paid'", "UPDATE")]
    [InlineData("DROP TABLE dbo.orders", "DROP")]
    [InlineData("EXEC sp_who2", "EXEC")]
    public void 書き込みの文は止まる(string sql, string expected) =>
        Assert.Equal(expected, ReadOnlyQueryGuard.FindWriteKeyword(sql));

    [Fact]
    public void 先頭が読み取りでも後ろの書き込みを見つける()
    {
        // 先頭の 1 文だけを見ると、この形が素通りしてしまう。
        Assert.Equal("DELETE", ReadOnlyQueryGuard.FindWriteKeyword("SELECT 1; DELETE FROM dbo.orders;"));
    }

    [Fact]
    public void 閉じていないコメントの後ろは読まない() =>
        Assert.Null(ReadOnlyQueryGuard.FindWriteKeyword("SELECT 1 /* DELETE FROM orders"));
}
