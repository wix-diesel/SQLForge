using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 起動直後に一覧を空にしないための見本データ。デザイン (Connect.dc.html) の
/// 保存済み接続と同じ並びで、保存機構（TOML + Secret Service）が入るまでの仮置き。
/// </summary>
public static class SeedConnections
{
    public static IEnumerable<ConnectionProfile> Create() =>
    [
        Profile("prod-analytics", EnvironmentTag.Production, DatabaseDriver.PostgreSql,
            "10.2.0.14", 5432, "sales_db", "analyst_ro", TlsMode.VerifyFull),
        Profile("prod-billing", EnvironmentTag.Production, DatabaseDriver.PostgreSql,
            "10.2.0.31", 5432, "billing", "billing_ro", TlsMode.VerifyFull),
        Profile("staging-eu", EnvironmentTag.Staging, DatabaseDriver.MySql,
            "stg.eu.internal", 3306, "shop", "app", TlsMode.Require),
        Profile("staging-warehouse", EnvironmentTag.Staging, DatabaseDriver.ClickHouse,
            "stg.eu.internal", 9000, "events", "reader", TlsMode.Require),
        Profile("local-cache", EnvironmentTag.Local, DatabaseDriver.Sqlite,
            "~/.cache/sqlforge.db", 0, "main", string.Empty, TlsMode.Disabled)
    ];

    private static ConnectionProfile Profile(
        string name,
        EnvironmentTag environment,
        DatabaseDriver driver,
        string host,
        int port,
        string database,
        string user,
        TlsMode tls)
    {
        var credentials = driver.IsFileBased
            ? ConnectionCredentials.Anonymous
            : new ConnectionCredentials(user, AuthenticationMethod.Password, true);

        return new ConnectionProfile(
            ConnectionProfileId.New(),
            name,
            environment,
            new ConnectionTarget(driver, new ServerAddress(host, port), database, tls),
            credentials,
            ConnectionProfile.DefaultAccessModeFor(environment));
    }
}
