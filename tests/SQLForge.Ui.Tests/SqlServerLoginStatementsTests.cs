using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ログインの追加・編集・削除の文面。識別子もパスワードもパラメータにできないので、
/// 引用符付けを通すこと、変わっていない項目は文面へ出さないことを確かめる。
/// </summary>
public class SqlServerLoginStatementsTests
{
    [Fact]
    public void SQL認証のログインはパスワードと規則を指定して作る()
    {
        var statements = SqlServerLoginStatements.Create(Definition());

        Assert.Equal(
            [
                "CREATE LOGIN [app_login] WITH PASSWORD = N'p@ssw0rd', CHECK_POLICY = ON, "
                    + "CHECK_EXPIRATION = ON, DEFAULT_DATABASE = [sales_db];"
            ],
            statements);
    }

    [Fact]
    public void 次回変更を求めるとMUST_CHANGEを付ける()
    {
        var statements = SqlServerLoginStatements.Create(Definition(mustChange: true));

        Assert.Equal(
            [
                "CREATE LOGIN [app_login] WITH PASSWORD = N'p@ssw0rd' MUST_CHANGE, CHECK_POLICY = ON, "
                    + "CHECK_EXPIRATION = ON, DEFAULT_DATABASE = [sales_db];"
            ],
            statements);
    }

    [Fact]
    public void 規則を外すとOFFで作る()
    {
        var statements = SqlServerLoginStatements.Create(
            Definition(policy: ServerLoginPasswordPolicy.None, database: null));

        Assert.Equal(
            ["CREATE LOGIN [app_login] WITH PASSWORD = N'p@ssw0rd', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;"],
            statements);
    }

    [Fact]
    public void Windows認証のログインはFROM_WINDOWSで作る()
    {
        var statements = SqlServerLoginStatements.Create(
            Definition(name: @"CONTOSO\app", type: ServerLoginType.WindowsUser, password: null));

        Assert.Equal([@"CREATE LOGIN [CONTOSO\app] FROM WINDOWS WITH DEFAULT_DATABASE = [sales_db];"], statements);
    }

    [Fact]
    public void Windowsグループも文面はFROM_WINDOWSのまま()
    {
        // ユーザーとグループの別は SID から決まるので、文面で書き分けることはできない。
        var statements = SqlServerLoginStatements.Create(
            Definition(name: @"CONTOSO\team", type: ServerLoginType.WindowsGroup, password: null, database: null));

        Assert.Equal([@"CREATE LOGIN [CONTOSO\team] FROM WINDOWS;"], statements);
    }

    [Fact]
    public void 無効にして作るときは作ったあとにDISABLEを流す()
    {
        var statements = SqlServerLoginStatements.Create(Definition() with { IsDisabled = true });

        Assert.Equal("ALTER LOGIN [app_login] DISABLE;", statements[^1]);
    }

    [Fact]
    public void サーバーロールは作成のあとにADD_MEMBERで足す()
    {
        var statements = SqlServerLoginStatements.Create(
            Definition() with { Roles = ["dbcreator", "processadmin"] });

        Assert.Equal(
            [
                "ALTER SERVER ROLE [dbcreator] ADD MEMBER [app_login];",
                "ALTER SERVER ROLE [processadmin] ADD MEMBER [app_login];"
            ],
            statements.Skip(1));
    }

    [Fact]
    public void 単引用符を含むパスワードは二重にして閉じられないようにする()
    {
        var statements = SqlServerLoginStatements.Create(
            Definition(password: "pa'ss--", policy: ServerLoginPasswordPolicy.None, database: null));

        Assert.Equal(
            ["CREATE LOGIN [app_login] WITH PASSWORD = N'pa''ss--', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;"],
            statements);
    }

    [Fact]
    public void 閉じ括弧を含む名前は二重にして閉じられないようにする()
    {
        var statements = SqlServerLoginStatements.Create(
            Definition(name: "we]ird", policy: ServerLoginPasswordPolicy.None, database: null));

        Assert.StartsWith("CREATE LOGIN [we]]ird] WITH", statements[0], StringComparison.Ordinal);
    }

    [Fact]
    public void SQL認証のログインをパスワード無しでは作らせない()
    {
        // 空のパスワードで作ると、誰でも入れるログインが黙って出来上がる。
        var rejected = Assert.Throws<ArgumentException>(
            () => SqlServerLoginStatements.Create(Definition(password: null)));

        Assert.StartsWith("SQL Server 認証のログインを作るにはパスワードが要ります。", rejected.Message);
    }

    [Fact]
    public void 削除はDROP_LOGIN()
    {
        Assert.Equal("DROP LOGIN [app_login];", SqlServerLoginStatements.Drop(new ServerLoginName("app_login")));
    }

    [Fact]
    public void 変わったところだけをALTER_LOGINに出す()
    {
        var statements = SqlServerLoginStatements.Alter(Original(), Definition(database: "audit_db", password: null));

        Assert.Equal(["ALTER LOGIN [app_login] WITH DEFAULT_DATABASE = [audit_db];"], statements);
    }

    [Fact]
    public void パスワードは入力があったときだけ文面に出す()
    {
        var statements = SqlServerLoginStatements.Alter(Original(), Definition(password: "new-p@ss"));

        Assert.Equal(["ALTER LOGIN [app_login] WITH PASSWORD = N'new-p@ss';"], statements);
    }

    [Fact]
    public void 規則はパスワードより先に流す()
    {
        // 順が逆だと、期限の適用を今から入れる編集で MUST_CHANGE がまだ通らない。
        var definition = Definition(password: "new-p@ss", mustChange: true);
        var original = Original() with { PasswordPolicy = ServerLoginPasswordPolicy.None };

        var statements = SqlServerLoginStatements.Alter(original, definition);

        Assert.Equal(
            [
                "ALTER LOGIN [app_login] WITH CHECK_POLICY = ON, CHECK_EXPIRATION = ON;",
                "ALTER LOGIN [app_login] WITH PASSWORD = N'new-p@ss' MUST_CHANGE;"
            ],
            statements);
    }

    [Fact]
    public void 有効と無効が変わったときだけENABLEかDISABLEを出す()
    {
        var disabled = SqlServerLoginStatements.Alter(
            Original(), Definition(password: null) with { IsDisabled = true });

        Assert.Equal(["ALTER LOGIN [app_login] DISABLE;"], disabled);

        var enabled = SqlServerLoginStatements.Alter(
            Original() with { IsDisabled = true }, Definition(password: null));

        Assert.Equal(["ALTER LOGIN [app_login] ENABLE;"], enabled);
    }

    [Fact]
    public void 名前を変えるとNAME句を出しロールの操作は新しい名前で行う()
    {
        var statements = SqlServerLoginStatements.Alter(
            Original() with { Roles = ["processadmin"] },
            Definition(name: "renamed", password: null) with { Roles = ["dbcreator"] });

        Assert.Equal(
            [
                "ALTER LOGIN [app_login] WITH NAME = [renamed];",
                "ALTER SERVER ROLE [dbcreator] ADD MEMBER [renamed];",
                "ALTER SERVER ROLE [processadmin] DROP MEMBER [renamed];"
            ],
            statements);
    }

    [Fact]
    public void 何も変わっていなければ文面を出さない()
    {
        Assert.Empty(SqlServerLoginStatements.Alter(Original(), Definition(password: null)));
    }

    private static ServerLoginDefinition Definition(
        string name = "app_login",
        ServerLoginType type = ServerLoginType.SqlLogin,
        string? password = "p@ssw0rd",
        ServerLoginPasswordPolicy? policy = null,
        bool mustChange = false,
        string? database = "sales_db") =>
        new(new ServerLoginName(name),
            type,
            password,
            policy ?? ServerLoginPasswordPolicy.Default,
            mustChange,
            database is null ? null : new DatabaseName(database));

    private static ServerLoginDescriptor Original() =>
        new(new ServerLoginName("app_login"), ServerLoginType.SqlLogin, new DatabaseName("sales_db"))
        {
            PasswordPolicy = ServerLoginPasswordPolicy.Default
        };
}
