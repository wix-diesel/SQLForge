namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 文字列リテラルの引用符付け。パスワードは識別子ではなく値だが、CREATE LOGIN も ALTER LOGIN も
/// パラメータを受け付けないので、文面へ直接書くしかない（SSMS が出す文面も同じ形になる）。
///
/// 単引用符を二重にして閉じられないようにし、照合順序の影響を受けないよう N を付けて Unicode で渡す。
/// </summary>
internal static class SqlServerLiteral
{
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
