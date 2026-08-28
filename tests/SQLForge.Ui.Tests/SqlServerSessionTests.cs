using SQLForge.Domain.Catalog;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// SQL Server のセッションのうち、接続を要らない組み立てだけを見る。
/// </summary>
public class SqlServerSessionTests
{
    [Fact]
    public void 先頭N行をのぞく文面はTOPで絞る()
    {
        var sql = new SqlServerSession(
                SeedConnections.Create().First(),
                new FakeDbConnection(),
                new ServerInfo("SQL Server 2022", "16.0.4215.2"))
            .BuildTopRowsQuery(new SchemaName("dbo"), "orders", 1000);

        Assert.Equal("SELECT TOP (1000) * FROM [dbo].[orders];", sql);
    }
}
