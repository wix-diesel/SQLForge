namespace SQLForge.Domain.Catalog;

/// <summary>
/// データベース内のスキーマ 1 件。
/// </summary>
/// <param name="Name">スキーマ名。</param>
/// <param name="IsSystem">エンジンが用意したスキーマ（SQL Server の sys / INFORMATION_SCHEMA など）。</param>
public sealed record SchemaDescriptor(SchemaName Name, bool IsSystem = false);
