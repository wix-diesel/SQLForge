using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ユーザー マッピングの文面。対応づけの実体は「そのデータベースの中のユーザー」なので、
/// データベースごとに分かれた文面になることを確かめる。
/// </summary>
public class SqlServerMappingStatementsTests
{
    private static readonly ServerLoginName Login = new("app_login");

    [Fact]
    public void 新しい対応づけはそのデータベースにユーザーを作る()
    {
        var plan = SqlServerMappingStatements.Plan(Login, original: [], desired: [Mapping("sales_db")]);

        var step = Assert.Single(plan);
        Assert.Equal("sales_db", step.Database.Value);
        Assert.Equal(
            [
                "CREATE USER [app_user] FOR LOGIN [app_login] WITH DEFAULT_SCHEMA = [sales];",
                "ALTER ROLE [db_datareader] ADD MEMBER [app_user];"
            ],
            step.Statements);
    }

    [Fact]
    public void 外した対応づけはそのデータベースのユーザーを消す()
    {
        var plan = SqlServerMappingStatements.Plan(Login, original: [Mapping("sales_db")], desired: []);

        var step = Assert.Single(plan);
        Assert.Equal("sales_db", step.Database.Value);
        Assert.Equal(["DROP USER [app_user];"], step.Statements);
    }

    [Fact]
    public void 変わっていない対応づけには何も出さない()
    {
        Assert.Empty(SqlServerMappingStatements.Plan(
            Login,
            original: [Mapping("sales_db")],
            desired: [Mapping("sales_db")]));
    }

    [Fact]
    public void ロールの出し入れだけならALTER_ROLEで足りる()
    {
        var plan = SqlServerMappingStatements.Plan(
            Login,
            original: [Mapping("sales_db")],
            desired: [Mapping("sales_db") with { Roles = ["db_datawriter"] }]);

        var step = Assert.Single(plan);
        Assert.Equal(
            [
                "ALTER ROLE [db_datawriter] ADD MEMBER [app_user];",
                "ALTER ROLE [db_datareader] DROP MEMBER [app_user];"
            ],
            step.Statements);
    }

    [Fact]
    public void データベースごとに別の文面になる()
    {
        var plan = SqlServerMappingStatements.Plan(
            Login,
            original: [Mapping("sales_db")],
            desired: [Mapping("staging_db")]);

        Assert.Equal(["staging_db", "sales_db"], plan.Select(step => step.Database.Value));
    }

    private static LoginUserMapping Mapping(string database) =>
        new(new DatabaseName(database), new DatabaseUserName("app_user"), new SchemaName("sales"))
        {
            Roles = ["db_datareader"]
        };
}
