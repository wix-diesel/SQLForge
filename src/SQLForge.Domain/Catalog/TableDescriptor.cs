namespace SQLForge.Domain.Catalog;

/// <summary>
/// スキーマ内のテーブル 1 件。
/// </summary>
/// <param name="Schema">属するスキーマ。</param>
/// <param name="Name">テーブル名。</param>
/// <param name="RowCount">おおよその行数。統計から取るので厳密ではない。取得できないときは null。</param>
/// <param name="CreatedAt">作成された日時。ツリーの絞り込み（作成日）に使う。読めないときは null。</param>
public sealed record TableDescriptor(
    SchemaName Schema,
    string Name,
    long? RowCount = null,
    DateTime? CreatedAt = null)
{
    /// <summary>スキーマ修飾した名前（例: dbo.orders）。</summary>
    public string QualifiedName => $"{Schema.Value}.{Name}";
}
