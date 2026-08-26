namespace SQLForge.Domain.Catalog;

/// <summary>
/// データベース内のスキーマ 1 件。
/// </summary>
/// <param name="Name">スキーマ名。</param>
/// <param name="IsSystem">エンジンが用意したスキーマ（SQL Server の sys / INFORMATION_SCHEMA など）。</param>
/// <param name="Owner">
/// スキーマの所有者（データベース ユーザーまたはデータベース ロール）。読めないときは null。
/// </param>
public sealed record SchemaDescriptor(SchemaName Name, bool IsSystem = false, string? Owner = null)
{
    /// <summary>この版で所有者を付け替えたり削除したりしてよいか。</summary>
    public bool IsEditable => !IsSystem;
}
