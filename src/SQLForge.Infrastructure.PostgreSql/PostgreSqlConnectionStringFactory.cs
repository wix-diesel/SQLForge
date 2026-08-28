using System.Data.Common;
using Npgsql;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.PostgreSql;

/// <summary>
/// 接続情報を PostgreSQL の接続文字列へ写す。
/// ここだけが「SQLForge の言葉」と「Npgsql の言葉」の対応表になっている。
///
/// SQL Server 側（<c>SqlServerConnectionStringFactory</c>）と受け持ちは同じだが、
/// 写し先が違う。ホストとポートは別の欄で、暗号化は Encrypt ではなく
/// libpq と同じ sslmode の段で表す。
/// </summary>
public static class PostgreSqlConnectionStringFactory
{
    /// <summary>Npgsql が受け付ける接続タイムアウトの上限（秒）。</summary>
    private const int MaxConnectTimeoutSeconds = 1024;

    public static string Build(ConnectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = request.Profile.Target;
        var advanced = request.Profile.Advanced;

        var builder = new NpgsqlConnectionStringBuilder
        {
            // SSH トンネルを通す接続では、繋ぎに行く先は踏み台ではなく手元の待ち受け口になる。
            Host = request.Endpoint.Host,
            Port = PortOf(request.Endpoint),
            Database = target.Database,
            ApplicationName = ConnectionUrl.ApplicationName,
            Timeout = ConnectTimeoutOf(request),

            // カタログを読むだけの用途で、セッションは 1 本を開きっぱなしにする。
            // プールに戻す相手がいないので切っておく。
            Pooling = false,

            // SSMS の「実行タイムアウト」。0 は待ち続ける、で Npgsql も同じ意味。
            CommandTimeout = advanced.ExecutionTimeoutSeconds
        };

        ApplyTls(builder, target.Tls, target.Certificate);
        ApplyCredentials(builder, request.Profile.Credentials, request.Secret);

        // 「詳細設定」タブに手で書いたものは、いちばん最後に写して他の欄より優先させる
        // （SSMS の [追加の接続パラメーター] と同じ）。
        ApplyAdditionalParameters(builder, advanced);

        return builder.ConnectionString;
    }

    /// <summary>
    /// ポートを持たない接続情報（欄を空のまま保存したもの）はドライバーの既定へ寄せる。
    /// Npgsql は 0 を受け付けない。
    /// </summary>
    private static int PortOf(ServerAddress address) =>
        address.HasPort ? address.Port : DatabaseDriver.PostgreSql.DefaultPort;

    /// <summary>
    /// 接続確立の待ち時間。「詳細設定」タブは 65535 秒まで受け付けるが、
    /// Npgsql の上限は 1024 秒なので、はみ出した指定はそこで止める。
    /// </summary>
    private static int ConnectTimeoutOf(ConnectionRequest request) =>
        Math.Clamp((int)Math.Ceiling(request.Timeout.TotalSeconds), 0, MaxConnectTimeoutSeconds);

    /// <summary>
    /// TLS 要求レベルの対応。段の名前が libpq の sslmode とほぼ同じなのでそのまま写せる。
    /// 「厳密」だけは PostgreSQL 17 の直接 TLS（TDS 8.0 に当たるもの）へ写す。
    /// </summary>
    private static void ApplyTls(
        NpgsqlConnectionStringBuilder builder,
        TlsMode tls,
        TlsCertificateSettings certificate)
    {
        builder.SslMode = tls switch
        {
            TlsMode.Disabled => SslMode.Disable,
            TlsMode.Prefer => SslMode.Prefer,

            // Npgsql の Require は「暗号化は必須、証明書は検証しない」。
            TlsMode.Require => SslMode.Require,
            TlsMode.VerifyFull => SslMode.VerifyFull,
            TlsMode.Strict => SslMode.VerifyFull,
            _ => SslMode.Prefer
        };

        if (tls == TlsMode.Strict)
        {
            // 接続直後に TLS を張る（PostgreSQL 17 以降）。それ以前のサーバーでは繋がらない。
            builder.SslNegotiation = SslNegotiation.Direct;
        }

        ApplyCertificate(builder, certificate);
    }

    /// <summary>
    /// 「TLS / SSL」タブ。サーバー証明書は libpq の sslrootcert に当たる欄へ写す。
    ///
    /// 証明書の中の名前を指す指定（SSMS の Host Name in Certificate）に当たるものは
    /// Npgsql に無い。黙って落とすと検証が通ったように見えるので、接続を試す前に断る。
    /// </summary>
    private static void ApplyCertificate(
        NpgsqlConnectionStringBuilder builder,
        TlsCertificateSettings certificate)
    {
        if (certificate.HasHostNameInCertificate)
        {
            throw new NotSupportedException(
                "PostgreSQL 接続では「証明書内のホスト名」を指定できません（Npgsql に相当する指定がありません）。");
        }

        if (certificate.HasServerCertificate)
        {
            builder.RootCertificate = certificate.ServerCertificatePath;
        }
    }

    private static void ApplyCredentials(
        NpgsqlConnectionStringBuilder builder,
        ConnectionCredentials credentials,
        string? secret)
    {
        switch (credentials.Method)
        {
            case AuthenticationMethod.Password:
                builder.Username = credentials.UserName;
                builder.Password = secret ?? string.Empty;
                break;

            case AuthenticationMethod.Integrated:
                // Npgsql には「統合認証を使う」指定が無い（7.0 で消えた）。利用者名と
                // パスワードを書かなければ、OS のアカウントで名乗り、サーバーが求めたときに
                // GSSAPI / SSPI で応じる。Linux では Kerberos の設定が要る。
                break;

            case AuthenticationMethod.Certificate:
                // クライアント証明書そのものの置き場所を接続情報が持っていないため写せない。
                throw new NotSupportedException("PostgreSQL 接続でのクライアント証明書認証は未対応です。");

            default:
                throw new NotSupportedException($"未知の認証方式です: {credentials.Method}");
        }
    }

    /// <summary>
    /// 手で書いた「キー=値;」をそのまま写す。読むのは基底の
    /// <see cref="DbConnectionStringBuilder"/> に任せる ―― 引用符や空白の扱いを
    /// 自前で数えずに済み、書いたキーだけが取り出せる。
    /// </summary>
    private static void ApplyAdditionalParameters(
        NpgsqlConnectionStringBuilder builder,
        AdvancedConnectionSettings advanced)
    {
        if (!advanced.HasAdditionalParameters)
        {
            return;
        }

        var extra = new DbConnectionStringBuilder();

        try
        {
            extra.ConnectionString = advanced.AdditionalParameters;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            // 壊れた書き方。接続を試みる前にここで分かる。
            throw Unreadable(exception.Message, exception);
        }

        foreach (var keyword in extra.Keys.Cast<string>())
        {
            // Npgsql は知らないキーを黙って捨てることがある。「書いたのに効かない」を
            // 作らないよう、写せるキーかどうかを先に見る。
            if (!builder.ContainsKey(keyword))
            {
                throw Unreadable($"知らないキーです（{keyword}）。");
            }

            try
            {
                builder[keyword] = extra[keyword];
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                // キーは知っているが、値がその欄の形に合わないもの。
                throw Unreadable(exception.Message, exception);
            }
        }
    }

    private static NotSupportedException Unreadable(string reason, Exception? inner = null) =>
        new($"追加の接続パラメーターを読めません: {reason}", inner);
}
