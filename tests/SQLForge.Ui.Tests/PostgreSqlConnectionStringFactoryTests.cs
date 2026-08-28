using Npgsql;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.PostgreSql;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 接続情報から PostgreSQL の接続文字列への写し方。
///
/// SQL Server との差がいちばん出るところなので、段の対応（TLS 要求レベルと sslmode）と、
/// Npgsql に無い指定の扱いをここで固定する。
/// </summary>
public class PostgreSqlConnectionStringFactoryTests
{
    [Fact]
    public void ホストとポートは別々の欄に書く()
    {
        // SQL Server の "host,port" と違い、PostgreSQL は Host と Port に分かれる。
        var builder = Build(port: 15432);

        Assert.Equal("db.internal", builder.Host);
        Assert.Equal(15432, builder.Port);
    }

    [Fact]
    public void ポートを持たない接続情報は既定ポートへ寄せる()
    {
        // Npgsql は 0 を受け付けない。欄を空のまま保存された接続情報の逃げ道。
        var builder = Build(port: 0);

        Assert.Equal(DatabaseDriver.PostgreSql.DefaultPort, builder.Port);
    }

    [Theory]
    [InlineData(TlsMode.Disabled, SslMode.Disable)]
    [InlineData(TlsMode.Prefer, SslMode.Prefer)]
    [InlineData(TlsMode.Require, SslMode.Require)]
    [InlineData(TlsMode.VerifyFull, SslMode.VerifyFull)]
    public void TLS要求レベルがsslmodeの段に対応する(TlsMode tls, SslMode expected)
    {
        var builder = Build(tls: tls);

        Assert.Equal(expected, builder.SslMode);
        Assert.Equal(SslNegotiation.Postgres, builder.SslNegotiation);
    }

    [Fact]
    public void 厳密は接続直後にTLSを張り証明書を必ず検証する()
    {
        // SQL Server の TDS 8.0 に当たるのが PostgreSQL 17 の直接 TLS。
        var builder = Build(tls: TlsMode.Strict);

        Assert.Equal(SslMode.VerifyFull, builder.SslMode);
        Assert.Equal(SslNegotiation.Direct, builder.SslNegotiation);
    }

    [Fact]
    public void サーバー証明書はルート証明書の欄へ写す()
    {
        var builder = Build(
            tls: TlsMode.VerifyFull,
            certificate: new TlsCertificateSettings { ServerCertificatePath = "/etc/ssl/certs/postgres.pem" });

        Assert.Equal("/etc/ssl/certs/postgres.pem", builder.RootCertificate);
    }

    [Fact]
    public void 証明書内のホスト名は写せないので接続前に断る()
    {
        // 黙って落とすと、検証が通ったように見えてしまう。
        var exception = Assert.Throws<NotSupportedException>(() => Build(
            tls: TlsMode.VerifyFull,
            certificate: new TlsCertificateSettings { HostNameInCertificate = "db.internal" }));

        Assert.Contains("ホスト名", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void パスワード認証では利用者名とパスワードを載せる()
    {
        var builder = Build(secret: "s3cret");

        Assert.Equal("analyst_ro", builder.Username);
        Assert.Equal("s3cret", builder.Password);
    }

    [Fact]
    public void OS統合認証では利用者名もパスワードも載せない()
    {
        // Npgsql に「統合認証を使う」指定は無く、名乗らなければ OS のアカウントで
        // GSSAPI / SSPI に応じる。
        var builder = Build(method: AuthenticationMethod.Integrated, secret: "s3cret");

        Assert.Null(builder.Username);
        Assert.Null(builder.Password);
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

        Assert.Equal("sales_db", builder.Database);
        Assert.Equal(ConnectionUrl.ApplicationName, builder.ApplicationName);
        Assert.Equal(15, builder.Timeout);
        Assert.False(builder.Pooling);
    }

    [Fact]
    public void 詳細設定の実行タイムアウトが載る()
    {
        var builder = Build(advanced: new AdvancedConnectionSettings { ExecutionTimeoutSeconds = 45 });

        Assert.Equal(45, builder.CommandTimeout);
    }

    [Fact]
    public void Npgsqlの上限を超える接続タイムアウトはそこで止める()
    {
        // 「詳細設定」タブは 65535 秒まで受け付けるが、Npgsql は 1024 秒までしか受け取らない。
        var builder = Build(timeout: 2000);

        Assert.Equal(1024, builder.Timeout);
    }

    [Fact]
    public void 追加の接続パラメーターは他の欄より後に写す()
    {
        var builder = Build(advanced: new AdvancedConnectionSettings
        {
            AdditionalParameters = "Application Name=other;Search Path=analytics"
        });

        Assert.Equal("other", builder.ApplicationName);
        Assert.Equal("analytics", builder.SearchPath);
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

    private static NpgsqlConnectionStringBuilder Build(
        int port = 5432,
        TlsMode tls = TlsMode.Require,
        AuthenticationMethod method = AuthenticationMethod.Password,
        string? secret = null,
        TlsCertificateSettings? certificate = null,
        AdvancedConnectionSettings? advanced = null,
        int? timeout = 15) =>
        new(PostgreSqlConnectionStringFactory.Build(
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
                DatabaseDriver.PostgreSql,
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
