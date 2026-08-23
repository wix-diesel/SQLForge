using System.Globalization;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// バージョン番号を人が読む名前にする。SERVERPROPERTY('ProductVersion') は
/// 16.0.4215.2 のような数字で、これだけでは何年の SQL Server か分からない。
/// </summary>
internal static class SqlServerProductName
{
    /// <summary>SERVERPROPERTY('EngineEdition') の値。数値の意味は SQL Server の仕様。</summary>
    private const int AzureSqlDatabase = 5;
    private const int AzureSqlManagedInstance = 8;
    private const int AzureSynapse = 6;
    private const int AzureSqlEdge = 9;

    private static readonly Dictionary<int, string> ReleaseNames = new()
    {
        [17] = "2025",
        [16] = "2022",
        [15] = "2019",
        [14] = "2017",
        [13] = "2016",
        [12] = "2014",
        [11] = "2012",
        [10] = "2008"
    };

    public static string Describe(string productVersion, int engineEdition) => engineEdition switch
    {
        AzureSqlDatabase => "Azure SQL Database",
        AzureSqlManagedInstance => "Azure SQL Managed Instance",
        AzureSynapse => "Azure Synapse Analytics",
        AzureSqlEdge => "Azure SQL Edge",
        _ => DescribeOnPremises(productVersion)
    };

    private static string DescribeOnPremises(string productVersion)
    {
        var major = ParseMajorVersion(productVersion);

        return major is not null && ReleaseNames.TryGetValue(major.Value, out var release)
            ? $"SQL Server {release}"
            : "SQL Server";
    }

    private static int? ParseMajorVersion(string productVersion)
    {
        var head = productVersion.Split('.', 2)[0];

        return int.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ? major : null;
    }
}
