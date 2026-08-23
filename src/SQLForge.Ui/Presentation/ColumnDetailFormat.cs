using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// カラムの右側に出す補足。SSMS のオブジェクトエクスプローラーに倣い、
/// 主キー・IDENTITY・型・NULL 許可の順で並べる（モックアップの「PK, int, not null」）。
/// </summary>
public static class ColumnDetailFormat
{
    public static string Describe(ColumnDescriptor column)
    {
        var parts = new List<string>();

        if (column.IsPrimaryKey)
        {
            parts.Add("PK");
        }

        if (column.IsIdentity)
        {
            parts.Add("IDENTITY");
        }

        parts.Add(column.DataType);
        parts.Add(column.IsNullable ? "null" : "not null");

        return string.Join(", ", parts);
    }
}
