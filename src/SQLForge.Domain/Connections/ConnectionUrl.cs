namespace SQLForge.Domain.Connections;

/// <summary>
/// 接続 URL の組み立て。ダイアログ下部に出す確認用の文字列で、
/// 実際の接続文字列としては使わない（ドライバー実装が入るときに置き換える）。
/// </summary>
public static class ConnectionUrl
{
    public const string ApplicationName = "sqlforge";

    public static string Build(ConnectionProfile profile) =>
        Build(profile.Target, profile.Credentials);

    public static string Build(ConnectionTarget target, ConnectionCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(credentials);

        if (target.Driver.IsFileBased)
        {
            return $"{target.Driver.UrlScheme}:{target.Address.Host}";
        }

        var authority = string.IsNullOrEmpty(credentials.UserName)
            ? target.Address.ToString()
            : $"{credentials.UserName}@{target.Address}";

        return $"{target.Driver.UrlScheme}://{authority}/{target.Database}?{BuildQuery(target)}";
    }

    private static string BuildQuery(ConnectionTarget target) =>
        string.Join('&', $"sslmode={ToSslMode(target.Tls)}", $"application_name={ApplicationName}");

    private static string ToSslMode(TlsMode mode) => mode switch
    {
        TlsMode.Disabled => "disable",
        TlsMode.Prefer => "prefer",
        TlsMode.Require => "require",
        TlsMode.VerifyFull => "verify-full",
        _ => "prefer"
    };
}
