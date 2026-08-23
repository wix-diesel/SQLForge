namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 識別子の引用符付け。データベース名はパラメータにできず SQL 文へ直接書くしかないので、
/// 角括弧で囲み、閉じ括弧を二重にして閉じられないようにする。
/// </summary>
internal static class SqlServerIdentifier
{
    /// <summary>SQL Server の識別子の上限（sysname = nvarchar(128)）。</summary>
    private const int MaxLength = 128;

    public static string Quote(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException("識別子は空にできません。", nameof(identifier));
        }

        if (identifier.Length > MaxLength)
        {
            throw new ArgumentException($"識別子が長すぎます（{MaxLength} 文字まで）。", nameof(identifier));
        }

        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }
}
