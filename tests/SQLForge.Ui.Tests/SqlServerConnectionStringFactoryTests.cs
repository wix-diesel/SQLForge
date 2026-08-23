using Microsoft.Data.SqlClient;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 接続情報から SQL Server の接続文字列への写し方。
/// ここを間違えると、暗号化していないつもりの接続を「TLS 有効」と誤って見せてしまう。
/// </summary>
public class SqlServerConnectionStringFactoryTests
{
    [Fact]
    public void ホストとポートはカンマ区切りで書く()
    {
        var builder = Build(port: 14330);

        Assert.Equal("db.internal,14330", builder.DataSource);
    }

    [Fact]
    public void 既定ポートならポートを書かない()
    {
        // 1433 を明示すると名前付きインスタンスの解決を邪魔することがあるので、既定なら省く。
        var builder = Build(port: 1433);

        Assert.Equal("db.internal", builder.DataSource);
    }

    [Theory]
    [InlineData(TlsMode.Disabled, false, true)]
    [InlineData(TlsMode.Prefer, false, true)]
    [InlineData(TlsMode.Require, true, true)]
    [InlineData(TlsMode.VerifyFull, true, false)]
    public void TLS要求レベルが暗号化と証明書検証に対応する(TlsMode tls, bool mandatory, bool trustCertificate)
    {
        var builder = Build(tls: tls);

        Assert.Equal(mandatory ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional, builder.Encrypt);
        Assert.Equal(trustCertificate, builder.TrustServerCertificate);
    }

    [Fact]
    public void パスワード認証では利用者名とパスワードを載せる()
    {
        var builder = Build(secret: "s3cret");

        Assert.False(builder.IntegratedSecurity);
        Assert.Equal("analyst_ro", builder.UserID);
        Assert.Equal("s3cret", builder.Password);
    }

    [Fact]
    public void OS統合認証では利用者名とパスワードを載せない()
    {
        var builder = Build(method: AuthenticationMethod.Integrated, secret: "s3cret");

        Assert.True(builder.IntegratedSecurity);
        Assert.Empty(builder.UserID);
        Assert.Empty(builder.Password);
    }

    [Fact]
    public void 証明書認証は未対応として弾く()
    {
        Assert.Throws<NotSupportedException>(() => Build(method: AuthenticationMethod.Certificate));
    }

    [Fact]
    public void データベース名とアプリ名と待ち時間が載る()
    {
        var builder = Build();

        Assert.Equal("sales_db", builder.InitialCatalog);
        Assert.Equal(ConnectionUrl.ApplicationName, builder.ApplicationName);
        Assert.Equal(15, builder.ConnectTimeout);
    }

    private static SqlConnectionStringBuilder Build(
        int port = 1433,
        TlsMode tls = TlsMode.Require,
        AuthenticationMethod method = AuthenticationMethod.Password,
        string? secret = null)
    {
        var profile = new ConnectionProfile(
            ConnectionProfileId.New(),
            "sqlforge-test",
            EnvironmentTag.Local,
            new ConnectionTarget(
                DatabaseDriver.SqlServer,
                new ServerAddress("db.internal", port),
                "sales_db",
                tls),
            new ConnectionCredentials("analyst_ro", method, storeSecretInKeyring: false),
            AccessMode.ReadWrite);

        var request = new ConnectionRequest(profile, secret) { Timeout = TimeSpan.FromSeconds(15) };

        return new SqlConnectionStringBuilder(SqlServerConnectionStringFactory.Build(request));
    }
}
