namespace SQLForge.Domain.Catalog;

/// <summary>
/// スキーマ内のストアド プロシージャ 1 件。
/// </summary>
/// <param name="Schema">属するスキーマ。</param>
/// <param name="Name">プロシージャ名。</param>
/// <param name="ParameterCount">戻り値を除いたパラメーターの数。</param>
public sealed record StoredProcedureDescriptor(SchemaName Schema, string Name, int ParameterCount = 0)
{
    /// <summary>スキーマ修飾した名前（例: dbo.usp_place_order）。</summary>
    public string QualifiedName => $"{Schema.Value}.{Name}";
}
