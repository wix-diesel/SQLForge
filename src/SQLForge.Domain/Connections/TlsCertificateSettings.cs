namespace SQLForge.Domain.Connections;

/// <summary>
/// TLS の証明書まわりの指定。SSMS の「Host Name in Certificate」と
/// 「サーバー証明書」に当たる。暗号化を要求するかどうか（信頼するかどうかを含む）は
/// <see cref="TlsMode"/> の担当で、ここはその検証に使う材料だけを持つ。
/// </summary>
public sealed record TlsCertificateSettings
{
    private readonly string _hostNameInCertificate = string.Empty;
    private readonly string _serverCertificatePath = string.Empty;

    /// <summary>何も指定していない状態。証明書はサーバーが出したものをそのまま見る。</summary>
    public static TlsCertificateSettings None { get; } = new();

    /// <summary>
    /// 証明書の CN / SAN と突き合わせる名前。繋ぎ先の名前（SSH トンネルごしの
    /// 127.0.0.1 など）と証明書に書かれた名前が食い違うときに指定する。
    /// </summary>
    public string HostNameInCertificate
    {
        get => _hostNameInCertificate;
        init => _hostNameInCertificate = Clean(value);
    }

    /// <summary>
    /// サーバー証明書のファイル（PEM / DER）。指定するとこの 1 枚とだけ突き合わせる。
    /// 社内 CA の証明書を OS の信頼ストアへ入れずに検証したいときに使う。
    /// </summary>
    public string ServerCertificatePath
    {
        get => _serverCertificatePath;
        init => _serverCertificatePath = Clean(value);
    }

    public bool HasHostNameInCertificate => HostNameInCertificate.Length > 0;

    public bool HasServerCertificate => ServerCertificatePath.Length > 0;

    /// <summary>何か指定されている状態。タブに印を出すのに使う。</summary>
    public bool IsConfigured => HasHostNameInCertificate || HasServerCertificate;

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
}
