namespace SQLForge.Domain.Connections;

/// <summary>
/// 接続に使うドライバー。既定ポートと URL スキームを持つ値オブジェクト。
/// 実際の接続処理は担わず、入力の既定値と接続 URL の組み立てにだけ使う。
/// </summary>
public sealed class DatabaseDriver : IEquatable<DatabaseDriver>
{
    public static readonly DatabaseDriver SqlServer = new("sqlserver", "SQL Server", "sqlserver", 1433, "master");
    public static readonly DatabaseDriver PostgreSql = new("postgres", "PostgreSQL", "postgresql", 5432, "postgres");
    public static readonly DatabaseDriver MySql = new("mysql", "MySQL", "mysql", 3306, "mysql");
    public static readonly DatabaseDriver ClickHouse = new("clickhouse", "ClickHouse", "clickhouse", 9000, "default");
    public static readonly DatabaseDriver Sqlite = new("sqlite", "SQLite", "sqlite", 0, "main");

    /// <summary>ドライバー選択の並び。SQLForge の主対象である SQL Server を先頭に置く。</summary>
    public static IReadOnlyList<DatabaseDriver> All { get; } =
        new[] { SqlServer, PostgreSql, MySql, ClickHouse, Sqlite };

    private DatabaseDriver(string id, string displayName, string urlScheme, int defaultPort, string defaultDatabase)
    {
        Id = id;
        DisplayName = displayName;
        UrlScheme = urlScheme;
        DefaultPort = defaultPort;
        DefaultDatabase = defaultDatabase;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string UrlScheme { get; }

    public int DefaultPort { get; }

    public string DefaultDatabase { get; }

    /// <summary>SQLite はファイルを開くだけなのでホスト・ポート・認証欄を持たない。</summary>
    public bool IsFileBased => Id == Sqlite.Id;

    public static DatabaseDriver FromId(string id) =>
        All.FirstOrDefault(driver => string.Equals(driver.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentOutOfRangeException(nameof(id), id, "未知のドライバーです。");

    public bool Equals(DatabaseDriver? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as DatabaseDriver);

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => DisplayName;
}
