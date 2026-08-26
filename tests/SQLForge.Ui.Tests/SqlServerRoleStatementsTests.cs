using SQLForge.Domain.Security;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ロールの追加・編集・削除の文面。識別子はパラメータにできないので、
/// 角括弧の引用符付けを通すこと、変わっていない項目は文面へ出さないことを確かめる。
/// </summary>
public class SqlServerRoleStatementsTests
{
    [Fact]
    public void データベースロールは所有者を添えて作る()
    {
        var statements = SqlServerDatabaseRoleStatements.Create(
            new DatabaseRoleDefinition(new RoleName("app_reader"), "dbo"));

        Assert.Equal(["CREATE ROLE [app_reader] AUTHORIZATION [dbo];"], statements);
    }

    [Fact]
    public void 所有者を指定しなければ文面にも出さない()
    {
        var statements = SqlServerDatabaseRoleStatements.Create(
            new DatabaseRoleDefinition(new RoleName("app_reader")));

        Assert.Equal(["CREATE ROLE [app_reader];"], statements);
    }

    [Fact]
    public void メンバーと所有スキーマは作成のあとに足す()
    {
        var statements = SqlServerDatabaseRoleStatements.Create(
            new DatabaseRoleDefinition(new RoleName("app_reader"))
            {
                Members = ["app_user"],
                OwnedSchemas = ["sales"]
            });

        Assert.Equal(
            [
                "CREATE ROLE [app_reader];",
                "ALTER ROLE [app_reader] ADD MEMBER [app_user];",
                "ALTER AUTHORIZATION ON SCHEMA::[sales] TO [app_reader];"
            ],
            statements);
    }

    [Fact]
    public void 名前を変えたあとのメンバー操作は新しい名前で行う()
    {
        var original = new DatabaseRoleDescriptor(new RoleName("app_reader"))
        {
            Members = ["old_user"]
        };

        var statements = SqlServerDatabaseRoleStatements.Alter(
            original,
            new DatabaseRoleDefinition(new RoleName("reporting")) { Members = ["new_user"] });

        Assert.Equal(
            [
                "ALTER ROLE [app_reader] WITH NAME = [reporting];",
                "ALTER ROLE [reporting] ADD MEMBER [new_user];",
                "ALTER ROLE [reporting] DROP MEMBER [old_user];"
            ],
            statements);
    }

    [Fact]
    public void 所有者を変えたときだけ所有権を移す()
    {
        var original = new DatabaseRoleDescriptor(new RoleName("app_reader"), "dbo");

        Assert.Empty(SqlServerDatabaseRoleStatements.Alter(
            original,
            new DatabaseRoleDefinition(new RoleName("app_reader"), "dbo")));

        Assert.Equal(
            ["ALTER AUTHORIZATION ON ROLE::[app_reader] TO [app_user];"],
            SqlServerDatabaseRoleStatements.Alter(
                original,
                new DatabaseRoleDefinition(new RoleName("app_reader"), "app_user")));
    }

    [Fact]
    public void 所有を外したスキーマはdboへ移す()
    {
        // スキーマは持ち主を空にできないので、外したぶんは SSMS と同じく dbo が引き取る。
        var original = new DatabaseRoleDescriptor(new RoleName("app_reader"))
        {
            OwnedSchemas = ["sales", "staging"]
        };

        var statements = SqlServerDatabaseRoleStatements.Alter(
            original,
            new DatabaseRoleDefinition(new RoleName("app_reader")) { OwnedSchemas = ["sales"] });

        Assert.Equal(["ALTER AUTHORIZATION ON SCHEMA::[staging] TO [dbo];"], statements);
    }

    [Fact]
    public void データベースロールはDROP_ROLEで消す()
    {
        Assert.Equal("DROP ROLE [app_reader];", SqlServerDatabaseRoleStatements.Drop(new RoleName("app_reader")));
    }

    [Fact]
    public void サーバーロールはメンバーとメンバーシップを両方向に足す()
    {
        var statements = SqlServerServerRoleStatements.Create(
            new ServerRoleDefinition(new RoleName("deployers"), "sa")
            {
                Members = ["app_login"],
                Memberships = ["dbcreator"]
            });

        Assert.Equal(
            [
                "CREATE SERVER ROLE [deployers] AUTHORIZATION [sa];",
                "ALTER SERVER ROLE [deployers] ADD MEMBER [app_login];",
                "ALTER SERVER ROLE [dbcreator] ADD MEMBER [deployers];"
            ],
            statements);
    }

    [Fact]
    public void サーバーロールのメンバーシップは外せる()
    {
        var original = new ServerRoleDescriptor(new RoleName("deployers"))
        {
            Memberships = ["dbcreator"]
        };

        var statements = SqlServerServerRoleStatements.Alter(
            original,
            new ServerRoleDefinition(new RoleName("deployers")));

        Assert.Equal(["ALTER SERVER ROLE [dbcreator] DROP MEMBER [deployers];"], statements);
    }

    [Fact]
    public void 閉じ括弧を含む名前は二重にして閉じられないようにする()
    {
        var statements = SqlServerDatabaseRoleStatements.Create(
            new DatabaseRoleDefinition(new RoleName("we]ird"), "ow]ner"));

        Assert.Equal(["CREATE ROLE [we]]ird] AUTHORIZATION [ow]]ner];"], statements);
    }
}
