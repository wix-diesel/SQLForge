using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// パラメーターの右側に出す補足。型・入出力の別・既定値の有無の順で並べる
/// （モックアップ「nvarchar(50), OUTPUT」に相当）。
/// </summary>
public static class StoredProcedureParameterDetailFormat
{
    public static string Describe(StoredProcedureParameterDescriptor parameter)
    {
        var parts = new List<string> { parameter.DataType, parameter.IsOutput ? "OUTPUT" : "IN" };

        if (parameter.HasDefaultValue)
        {
            parts.Add("既定値あり");
        }

        return string.Join(", ", parts);
    }
}
