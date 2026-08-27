using Microsoft.Data.SqlClient;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.SqlServer;
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
    public void 厳密はTDS8で張り証明書を必ず検証する()
    {
        // SSMS の [暗号化] = Strict に当たる段。信頼するかの指定はそもそも効かない。
        var builder = Build(tls: TlsMode.Strict);

        Assert.Equal(SqlConnectionEncryptOption.Strict, builder.Encrypt);
        Assert.False(builder.TrustServerCertificate);
    }

    [Fact]
    public void 証明書の指定が接続文字列に載る()
    {
        var builder = Build(
            tls: TlsMode.VerifyFull,
            certificate: new TlsCertificateSettings
            {
                HostNameInCertificate = "db.internal",
                ServerCertificatePath = "/etc/ssl/certs/sqlserver.pem"
            });

        Assert.Equal("db.internal", builder.HostNameInCertificate);
        Assert.Equal("/etc/ssl/certs/sqlserver.pem", builder.ServerCertificate);
    }

    [Fact]
    public void 証明書を指定しなければ何も載せない()
    {
        var builder = Build();

        Assert.Empty(builder.HostNameInCertificate);
        Assert.Empty(builder.ServerCertificate);
    }

    [Fact]
    public void 詳細設定のパケットサイズと実行タイムアウトが載る()
    {
        var builder = Build(advanced: new AdvancedConnectionSettings
        {
            PacketSize = 8192,
            ConnectTimeoutSeconds = 30,
            ExecutionTimeoutSeconds = 45
        });

        Assert.Equal(8192, builder.PacketSize);
        Assert.Equal(45, builder.CommandTimeout);
    }

    [Fact]
    public void 詳細設定の接続タイムアウトが待ち時間になる()
    {
        // 待ち時間の既定は「詳細設定」タブの値。SSMS と同じ 15 秒。
        var builder = Build(advanced: new AdvancedConnectionSettings { ConnectTimeoutSeconds = 30 }, timeout: null);

        Assert.Equal(30, builder.ConnectTimeout);
    }

    [Theory]
    [InlineData(NetworkProtocol.Default, "db.internal,14330")]
    [InlineData(NetworkProtocol.TcpIp, "tcp:db.internal,14330")]
    [InlineData(NetworkProtocol.NamedPipes, "np:db.internal,14330")]
    [InlineData(NetworkProtocol.SharedMemory, "lpc:db.internal,14330")]
    public void ネットワークプロトコルは接頭辞で書く(NetworkProtocol protocol, string expected)
    {
        var builder = Build(port: 14330, advanced: new AdvancedConnectionSettings { Protocol = protocol });

        Assert.Equal(expected, builder.DataSource);
    }

    [Fact]
    public void 追加の接続パラメーターは他の欄より後に写す()
    {
        // SSMS と同じで、手で書いたものが勝つ。
        var builder = Build(advanced: new AdvancedConnectionSettings
        {
            AdditionalParameters = "Application Name=other;ApplicationIntent=ReadOnly"
        });

        Assert.Equal("other", builder.ApplicationName);
        Assert.Equal(ApplicationIntent.ReadOnly, builder.ApplicationIntent);
    }

    [Fact]
    public void 読めない追加の接続パラメーターは接続前に理由を返す()
    {
        var exception = Assert.Throws<NotSupportedException>(() => Build(advanced: new AdvancedConnectionSettings
        {
            AdditionalParameters = "NoSuchKeyword=1"
        }));

        Assert.Contains("追加の接続パラメーター", exception.Message, StringComparison.Ordinal);
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
        string? secret = null,
        TlsCertificateSettings? certificate = null,
        AdvancedConnectionSettings? advanced = null,
        int? timeout = 15) =>
        new(SqlServerConnectionStringFactory.Build(
            Request(port, tls, method, secret, certificate, advanced, timeout)));

    private static ConnectionRequest Request(
        int port,
        TlsMode tls,
        AuthenticationMethod method,
        string? secret,
        TlsCertificateSettings? certificate,
        AdvancedConnectionSettings? advanced,
        int? timeout)
    {
        var profile = new ConnectionProfile(
            ConnectionProfileId.New(),
            "sqlforge-test",
            EnvironmentTag.Local,
            new ConnectionTarget(
                DatabaseDriver.SqlServer,
                new ServerAddress("db.internal", port),
                "sales_db",
                tls,
                certificate),
            new ConnectionCredentials("analyst_ro", method, storeSecretInKeyring: false),
            AccessMode.ReadWrite,
            advanced: advanced);

        var request = new ConnectionRequest(profile, secret);

        return timeout is { } seconds ? request with { Timeout = TimeSpan.FromSeconds(seconds) } : request;
    }
}
