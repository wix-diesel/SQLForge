using System.Data.Common;
using Microsoft.Data.SqlClient;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 接続情報を SQL Server の接続文字列へ写す。
/// ここだけが「SQLForge の言葉」と「SqlClient の言葉」の対応表になっている。
/// </summary>
public static class SqlServerConnectionStringFactory
{
    public static string Build(ConnectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = request.Profile.Target;
        var advanced = request.Profile.Advanced;

        var builder = new SqlConnectionStringBuilder
        {
            // SSH トンネルを通す接続では、繋ぎに行く先は踏み台ではなく手元の待ち受け口になる。
            DataSource = BuildDataSource(request.Endpoint, advanced.Protocol),
            InitialCatalog = target.Database,
            ApplicationName = ConnectionUrl.ApplicationName,
            ConnectTimeout = (int)Math.Ceiling(request.Timeout.TotalSeconds),

            // カタログを読むだけの用途で、セッションは 1 本を開きっぱなしにする。
            // プールに戻す相手がいないので切っておく。
            Pooling = false
        };

        ApplyAdvanced(builder, advanced);
        ApplyTls(builder, target.Tls, target.Certificate);
        ApplyCredentials(builder, request.Profile.Credentials, request.Secret);

        // 「詳細設定」タブに手で書いたものは、いちばん最後に写して他の欄より優先させる
        // （SSMS の [追加の接続パラメーター] と同じ）。
        ApplyAdditionalParameters(builder, advanced);

        return builder.ConnectionString;
    }

    /// <summary>
    /// SQL Server は host,port の形で書く（コロンではない）。既定ポートのときは書かない
    /// ―― 明示すると名前付きインスタンスの解決を邪魔することがある。
    /// プロトコルを選んでいれば、その接頭辞（tcp: / np: / lpc:）を頭に付ける。
    /// </summary>
    private static string BuildDataSource(ServerAddress address, NetworkProtocol protocol)
    {
        var host = address.HasPort && address.Port != DatabaseDriver.SqlServer.DefaultPort
            ? $"{address.Host},{address.Port}"
            : address.Host;

        return ProtocolPrefix(protocol) + host;
    }

    private static string ProtocolPrefix(NetworkProtocol protocol) => protocol switch
    {
        NetworkProtocol.TcpIp => "tcp:",
        NetworkProtocol.NamedPipes => "np:",
        NetworkProtocol.SharedMemory => "lpc:",
        _ => string.Empty
    };

    /// <summary>「詳細設定」タブ。名前も既定値も SSMS の [接続プロパティ] に合わせてある。</summary>
    private static void ApplyAdvanced(SqlConnectionStringBuilder builder, AdvancedConnectionSettings advanced)
    {
        builder.PacketSize = advanced.PacketSize;

        // SSMS の「実行タイムアウト」。0 は待ち続ける、で SqlClient も同じ意味。
        builder.CommandTimeout = advanced.ExecutionTimeoutSeconds;
    }

    /// <summary>
    /// TLS 要求レベルの対応。SqlClient には「証明書は検証しないが暗号化は必須」という
    /// 段があるので、prefer / require / verify-full をそこへ写す。
    /// 「厳密」は TDS 8.0（Strict）で、証明書を信頼するかの指定はそもそも効かない。
    /// </summary>
    private static void ApplyTls(
        SqlConnectionStringBuilder builder,
        TlsMode tls,
        TlsCertificateSettings certificate)
    {
        switch (tls)
        {
            case TlsMode.Disabled:
            case TlsMode.Prefer:
                // SqlClient には「使わない」がなく、サーバーが要求すれば必ず張られる。
                // クライアント側から必須にしない、が Optional の意味。
                builder.Encrypt = SqlConnectionEncryptOption.Optional;
                builder.TrustServerCertificate = true;
                break;

            case TlsMode.Require:
                builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
                builder.TrustServerCertificate = true;
                break;

            case TlsMode.VerifyFull:
                builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
                builder.TrustServerCertificate = false;
                break;

            case TlsMode.Strict:
                builder.Encrypt = SqlConnectionEncryptOption.Strict;
                builder.TrustServerCertificate = false;
                break;

            default:
                builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
                builder.TrustServerCertificate = true;
                break;
        }

        ApplyCertificate(builder, certificate);
    }

    /// <summary>
    /// 「TLS / SSL」タブ。証明書を検証しない要求レベルでも指定はそのまま写す
    /// ―― 要求レベルを上げたときに、指定し直さなくても効くようにするため。
    /// </summary>
    private static void ApplyCertificate(SqlConnectionStringBuilder builder, TlsCertificateSettings certificate)
    {
        if (certificate.HasHostNameInCertificate)
        {
            builder.HostNameInCertificate = certificate.HostNameInCertificate;
        }

        if (certificate.HasServerCertificate)
        {
            builder.ServerCertificate = certificate.ServerCertificatePath;
        }
    }

    private static void ApplyCredentials(
        SqlConnectionStringBuilder builder,
        ConnectionCredentials credentials,
        string? secret)
    {
        switch (credentials.Method)
        {
            case AuthenticationMethod.Password:
                builder.IntegratedSecurity = false;
                builder.UserID = credentials.UserName;
                builder.Password = secret ?? string.Empty;
                break;

            case AuthenticationMethod.Integrated:
                // Linux では Kerberos の設定が要る。整っていなければ接続時に SqlClient が理由を返す。
                builder.IntegratedSecurity = true;
                break;

            case AuthenticationMethod.Certificate:
                throw new NotSupportedException("SQL Server 接続でのクライアント証明書認証は未対応です。");

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
        SqlConnectionStringBuilder builder,
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

            foreach (var keyword in extra.Keys.Cast<string>())
            {
                builder[keyword] = extra[keyword];
            }
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            // 知らないキーや壊れた書き方。接続を試みる前にここで分かる。
            throw new NotSupportedException(
                $"追加の接続パラメーターを読めません: {exception.Message}",
                exception);
        }
    }
}
