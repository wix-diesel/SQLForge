namespace SQLForge.Domain.Connections;

/// <summary>
/// 「どこに繋ぐか」。ドライバー・アドレス・データベース・TLS 要求と、
/// その検証に使う証明書の指定をまとめた値オブジェクト。
/// </summary>
public sealed record ConnectionTarget
{
    public ConnectionTarget(
        DatabaseDriver driver,
        ServerAddress address,
        string database,
        TlsMode tls = TlsMode.Prefer,
        TlsCertificateSettings? certificate = null)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Address = address ?? throw new ArgumentNullException(nameof(address));
        Database = string.IsNullOrWhiteSpace(database) ? driver.DefaultDatabase : database.Trim();
        Tls = tls;
        Certificate = certificate ?? TlsCertificateSettings.None;
    }

    public DatabaseDriver Driver { get; }

    public ServerAddress Address { get; }

    public string Database { get; }

    public TlsMode Tls { get; }

    /// <summary>証明書の検証に使う材料（「TLS / SSL」タブ）。指定が無ければ既定のまま検証する。</summary>
    public TlsCertificateSettings Certificate { get; }

    /// <summary>保存済み接続の一覧に出す 1 行の要約（例: postgres · 10.2.0.14:5432）。</summary>
    public string Summary => $"{Driver.Id} · {Address}";
}
