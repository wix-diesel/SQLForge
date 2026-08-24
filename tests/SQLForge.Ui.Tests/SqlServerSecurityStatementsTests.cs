using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ユーザーの追加・編集・削除の文面。識別子はパラメータにできないので、
/// 角括弧の引用符付けを通すこと、変わっていない項目は文面へ出さないことを確かめる。
/// </summary>
public class SqlServerSecurityStatementsTests
{
    [Fact]
    public void ログインありのユーザーはFOR_LOGINで作る()
    {
        var statements = SqlServerSecurityStatements.Create(Definition());

        Assert.Equal(
            ["CREATE USER [app_user] FOR LOGIN [app_login] WITH DEFAULT_SCHEMA = [sales];"],
            statements);
    }

    [Fact]
    public void ログインなしのユーザーはWITHOUT_LOGINで作る()
    {
        var statements = SqlServerSecurityStatements.Create(
            Definition(type: DatabaseUserType.SqlUserWithoutLogin, login: null));

        Assert.Equal(
            ["CREATE USER [app_user] WITHOUT LOGIN WITH DEFAULT_SCHEMA = [sales];"],
            statements);
    }

    [Fact]
    public void 既定のスキーマを指定しなければ文面にも出さない()
    {
        var statements = SqlServerSecurityStatements.Create(Definition(schema: null));

        Assert.Equal(["CREATE USER [app_user] FOR LOGIN [app_login];"], statements);
    }

    [Fact]
    public void ロールは作成のあとにADD_MEMBERで足す()
    {
        var statements = SqlServerSecurityStatements.Create(
            Definition() with { Roles = ["db_datareader", "db_datawriter"] });

        Assert.Equal(
            [
                "CREATE USER [app_user] FOR LOGIN [app_login] WITH DEFAULT_SCHEMA = [sales];",
                "ALTER ROLE [db_datareader] ADD MEMBER [app_user];",
                "ALTER ROLE [db_datawriter] ADD MEMBER [app_user];"
            ],
            statements);
    }

    [Fact]
    public void 閉じ括弧を含む名前は二重にして閉じられないようにする()
    {
        var statements = SqlServerSecurityStatements.Create(Definition(name: "we]ird", schema: null));

        Assert.Equal(["CREATE USER [we]]ird] FOR LOGIN [app_login];"], statements);
    }

    [Fact]
    public void 変わったところだけをALTER_USERに出す()
    {
        var statements = SqlServerSecurityStatements.Alter(Original(), Definition(schema: "audit"));

        Assert.Equal(["ALTER USER [app_user] WITH DEFAULT_SCHEMA = [audit];"], statements);
    }

    [Fact]
    public void 名前を変えるとNAME句を出しロールの操作は新しい名前で行う()
    {
        var statements = SqlServerSecurityStatements.Alter(
            Original() with { Roles = ["db_datawriter"] },
            Definition(name: "renamed") with { Roles = ["db_datareader"] });

        Assert.Equal(
            [
                "ALTER USER [app_user] WITH NAME = [renamed];",
                "ALTER ROLE [db_datareader] ADD MEMBER [renamed];",
                "ALTER ROLE [db_datawriter] DROP MEMBER [renamed];"
            ],
            statements);
    }

    [Fact]
    public void 既定のスキーマを消すとNULLを指定する()
    {
        var statements = SqlServerSecurityStatements.Alter(Original(), Definition(schema: null));

        Assert.Equal(["ALTER USER [app_user] WITH DEFAULT_SCHEMA = NULL;"], statements);
    }

    [Fact]
    public void ログインを付け替えるとLOGIN句を出す()
    {
        var statements = SqlServerSecurityStatements.Alter(Original(), Definition(login: "other_login"));

        Assert.Equal(["ALTER USER [app_user] WITH LOGIN = [other_login];"], statements);
    }

    [Fact]
    public void 何も変わっていなければ文面を出さない()
    {
        Assert.Empty(SqlServerSecurityStatements.Alter(Original(), Definition()));
    }

    [Fact]
    public void ログインが要る種類でログイン名が無ければ作らせない()
    {
        // 黙って WITHOUT LOGIN の文面へ倒れると、頼んだのとは別の種類のユーザーが出来上がる。
        var rejected = Assert.Throws<ArgumentException>(
            () => Definition(type: DatabaseUserType.WindowsUser, login: " "));

        Assert.StartsWith("Windows ユーザー にはログイン名が要ります。", rejected.Message);
    }

    [Fact]
    public void ログインを取らない種類ではログイン名を持たせない()
    {
        // 種類を切り替えたあとに前の入力が残っていても、文面へは持ち出さない。
        var definition = Definition(type: DatabaseUserType.SqlUserWithoutLogin, login: "app_login");

        Assert.Null(definition.LoginName);
        Assert.Equal(
            ["CREATE USER [app_user] WITHOUT LOGIN WITH DEFAULT_SCHEMA = [sales];"],
            SqlServerSecurityStatements.Create(definition));
    }

    [Fact]
    public void 削除はDROP_USER()
    {
        Assert.Equal("DROP USER [app_user];", SqlServerSecurityStatements.Drop(new DatabaseUserName("app_user")));
    }

    private static DatabaseUserDefinition Definition(
        string name = "app_user",
        DatabaseUserType type = DatabaseUserType.SqlUserWithLogin,
        string? login = "app_login",
        string? schema = "sales") =>
        new(new DatabaseUserName(name),
            type,
            login,
            schema is null ? null : new SchemaName(schema));

    private static DatabaseUserDescriptor Original() =>
        new(new DatabaseUserName("app_user"),
            DatabaseUserType.SqlUserWithLogin,
            "app_login",
            new SchemaName("sales"));
}
