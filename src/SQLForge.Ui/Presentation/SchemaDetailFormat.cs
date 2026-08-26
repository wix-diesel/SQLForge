using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// ツリーのスキーマ行の右端に出す補足。所有者が読めたときだけ出す
/// （読めないことは権限しだいで普通にある）。
/// </summary>
public static class SchemaDetailFormat
{
    public static string? Describe(SchemaDescriptor schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return string.IsNullOrEmpty(schema.Owner) ? null : $"所有者 {schema.Owner}";
    }
}
