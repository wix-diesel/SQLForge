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
        var credentials = request.Profile.Credentials;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = BuildDataSource(target.Address),
            InitialCatalog = target.Database,
            ApplicationName = ConnectionUrl.ApplicationName,
            ConnectTimeout = (int)Math.Ceiling(request.Timeout.TotalSeconds),

            // カタログを読むだけの用途で、セッションは 1 本を開きっぱなしにする。
            // プールに戻す相手がいないので切っておく。
            Pooling = false
        };

        ApplyTls(builder, target.Tls);
        ApplyCredentials(builder, credentials, request.Secret);

        return builder.ConnectionString;
    }

    /// <summary>
    /// SQL Server は host,port の形で書く（コロンではない）。既定ポートのときは書かない
    /// ―― 明示すると名前付きインスタンスの解決を邪魔することがある。
    /// </summary>
    private static string BuildDataSource(ServerAddress address) =>
        address.HasPort && address.Port != DatabaseDriver.SqlServer.DefaultPort
            ? $"{address.Host},{address.Port}"
            : address.Host;

    /// <summary>
    /// TLS 要求レベルの対応。SqlClient には「証明書は検証しないが暗号化は必須」という
    /// 段があるので、prefer / require / verify-full をそこへ写す。
    /// </summary>
    private static void ApplyTls(SqlConnectionStringBuilder builder, TlsMode tls)
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

            default:
                builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
                builder.TrustServerCertificate = true;
                break;
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
}
