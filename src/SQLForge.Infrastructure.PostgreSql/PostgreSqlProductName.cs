using System.Globalization;

namespace SQLForge.Infrastructure.PostgreSql;

/// <summary>
/// バージョン番号を人が読む名前にする。PostgreSQL の版は年号ではなく
/// メジャー番号そのもの（16 / 17）で呼ばれるので、頭の数字だけを添える。
///
/// 派生（Amazon Aurora・CockroachDB など）は version() の 1 行に名乗りが入るので、
/// 分かるものはその名前を出す。
/// </summary>
internal static class PostgreSqlProductName
{
    /// <summary>version() の頭に出てくる名乗りと、画面に出す名前。</summary>
    private static readonly (string Banner, string Product)[] Distributions =
    [
        ("CockroachDB", "CockroachDB"),
        ("YugabyteDB", "YugabyteDB"),
        ("Greenplum", "Greenplum")
    ];

    /// <param name="serverVersion">current_setting('server_version')。例: 16.2</param>
    /// <param name="banner">version() の 1 行。</param>
    public static string Describe(string serverVersion, string banner)
    {
        foreach (var (name, product) in Distributions)
        {
            if (banner.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return product;
            }
        }

        var major = ParseMajorVersion(serverVersion);

        return major is null ? "PostgreSQL" : $"PostgreSQL {major}";
    }

    private static int? ParseMajorVersion(string serverVersion)
    {
        var head = serverVersion.Split('.', 2)[0];

        // 開発版は "18devel" のように数字のあとが続く。数字の並びだけを見る。
        var digits = new string(head.TakeWhile(char.IsAsciiDigit).ToArray());

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ? major : null;
    }
}
