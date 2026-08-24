using SQLForge.Domain.Connections;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ダイアログ下部に出す確認用の接続 URL。実際に張られる接続と食い違って見えると
/// 確認の役に立たないので、認証方式の違いもここに出す。
/// </summary>
public class ConnectionUrlTests
{
    [Fact]
    public void パスワード認証では利用者名を権限部に載せる()
    {
        var url = Build(new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, false));

        Assert.Equal(
            "sqlserver://analyst_ro@db.internal:1433/sales_db?sslmode=require&application_name=sqlforge",
            url);
    }

    [Fact]
    public void OS統合認証では利用者名を載せず統合認証と示す()
    {
        // 名乗る相手は OS が決めるので、権限部に書ける利用者名が無い。
        // 代わりに、どの認証方式で繋ぐつもりなのかをクエリに出す。
        var url = Build(new ConnectionCredentials("analyst_ro", AuthenticationMethod.Integrated, false));

        Assert.Equal(
            "sqlserver://db.internal:1433/sales_db?sslmode=require&integrated_security=true&application_name=sqlforge",
            url);
    }

    private static string Build(ConnectionCredentials credentials) =>
        ConnectionUrl.Build(
            new ConnectionTarget(
                DatabaseDriver.SqlServer,
                new ServerAddress("db.internal", 1433),
                "sales_db",
                TlsMode.Require),
            credentials);
}
