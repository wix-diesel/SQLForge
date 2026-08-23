namespace SQLForge.Domain.Catalog;

/// <summary>
/// テーブル 1 列。
/// </summary>
/// <param name="Name">列名。</param>
/// <param name="OrdinalPosition">テーブル定義での並び順（1 始まり）。</param>
/// <param name="DataType">型の表示名（例: nvarchar(50)、decimal(18,2)）。</param>
/// <param name="IsNullable">NULL を許すか。</param>
/// <param name="IsIdentity">IDENTITY 列か。</param>
/// <param name="IsPrimaryKey">主キーの一部か。</param>
public sealed record ColumnDescriptor(
    string Name,
    int OrdinalPosition,
    string DataType,
    bool IsNullable,
    bool IsIdentity,
    bool IsPrimaryKey);
