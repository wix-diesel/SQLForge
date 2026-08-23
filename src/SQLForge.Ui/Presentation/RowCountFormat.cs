using System.Globalization;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// 行数の見せ方。ツリーの行は狭いので、桁が増えたら丸める（モックアップの「8.4M 行」）。
/// 統計から取った概算なので、丸めても意味が変わらない。
/// </summary>
public static class RowCountFormat
{
    private const long Thousand = 1_000;
    private const long Million = 1_000_000;
    private const long Billion = 1_000_000_000;

    public static string? Describe(long? rowCount) =>
        rowCount is { } count && count >= 0 ? $"{Scale(count)} 行" : null;

    private static string Scale(long count) => count switch
    {
        >= Billion => Format(count / (double)Billion, "B"),
        >= Million => Format(count / (double)Million, "M"),
        >= 10 * Thousand => Format(count / (double)Thousand, "k"),
        _ => count.ToString("N0", CultureInfo.InvariantCulture)
    };

    private static string Format(double value, string suffix) =>
        value.ToString(value < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + suffix;
}
