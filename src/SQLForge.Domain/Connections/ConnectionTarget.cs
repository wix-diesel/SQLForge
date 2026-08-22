namespace SQLForge.Domain.Connections;

/// <summary>「どこに繋ぐか」。ドライバー・アドレス・データベース・TLS 要求をまとめた値オブジェクト。</summary>
public sealed record ConnectionTarget
{
    public ConnectionTarget(
        DatabaseDriver driver,
        ServerAddress address,
        string database,
        TlsMode tls = TlsMode.Prefer)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Address = address ?? throw new ArgumentNullException(nameof(address));
        Database = string.IsNullOrWhiteSpace(database) ? driver.DefaultDatabase : database.Trim();
        Tls = tls;
    }

    public DatabaseDriver Driver { get; }

    public ServerAddress Address { get; }

    public string Database { get; }

    public TlsMode Tls { get; }

    /// <summary>保存済み接続の一覧に出す 1 行の要約（例: postgres · 10.2.0.14:5432）。</summary>
    public string Summary => $"{Driver.Id} · {Address}";
}
