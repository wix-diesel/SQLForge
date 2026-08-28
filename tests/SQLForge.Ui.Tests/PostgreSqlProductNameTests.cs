using SQLForge.Infrastructure.PostgreSql;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ステータスバーと接続テストに出す製品名の組み立て。
/// </summary>
public class PostgreSqlProductNameTests
{
    [Theory]
    [InlineData("16.2", "PostgreSQL 16")]
    [InlineData("17.0", "PostgreSQL 17")]
    [InlineData("18devel", "PostgreSQL 18")]
    public void メジャー番号を名前に添える(string serverVersion, string expected)
    {
        Assert.Equal(expected, PostgreSqlProductName.Describe(serverVersion, "PostgreSQL " + serverVersion));
    }

    [Fact]
    public void 版が読めなければ製品名だけを出す()
    {
        Assert.Equal("PostgreSQL", PostgreSqlProductName.Describe(string.Empty, string.Empty));
    }

    [Fact]
    public void 互換エンジンは名乗りのほうを出す()
    {
        // PostgreSQL の口を借りているだけの別物なので、版を足すとかえって紛らわしい。
        var name = PostgreSqlProductName.Describe(
            "13.0.0",
            "CockroachDB CCL v24.1.0 (x86_64-pc-linux-gnu)");

        Assert.Equal("CockroachDB", name);
    }
}
