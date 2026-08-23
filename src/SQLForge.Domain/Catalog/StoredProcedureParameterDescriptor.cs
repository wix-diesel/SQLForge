namespace SQLForge.Domain.Catalog;

/// <summary>
/// ストアド プロシージャ 1 個のパラメーター。戻り値（parameter_id = 0）は含まない。
/// </summary>
/// <param name="Name">パラメーター名（@ を含む）。</param>
/// <param name="OrdinalPosition">宣言順（1 始まり）。</param>
/// <param name="DataType">型の表示名（例: nvarchar(50)、decimal(18,2)）。</param>
/// <param name="IsOutput">OUTPUT パラメーターか。</param>
/// <param name="HasDefaultValue">既定値を持つか。</param>
public sealed record StoredProcedureParameterDescriptor(
    string Name,
    int OrdinalPosition,
    string DataType,
    bool IsOutput,
    bool HasDefaultValue);
