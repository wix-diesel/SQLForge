namespace SQLForge.Domain.Sql;

/// <summary>
/// 文面の FROM / JOIN に出てくるテーブル 1 つ。列の補完で
/// 「別名 → どのテーブルか」を解くのに使う。
/// </summary>
/// <param name="Schema">スキーマ名。修飾されていなければ null。</param>
/// <param name="Name">テーブル名。</param>
/// <param name="Alias">別名。付いていなければ null。</param>
public sealed record SqlTableReference(string? Schema, string Name, string? Alias)
{
    /// <summary>o. の o がこのテーブルを指しているか。別名が無ければテーブル名で受ける。</summary>
    public bool Matches(string qualifier) =>
        string.Equals(Alias ?? Name, qualifier, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Name, qualifier, StringComparison.OrdinalIgnoreCase);
}
