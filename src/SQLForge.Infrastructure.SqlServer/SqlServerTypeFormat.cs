using System.Globalization;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// カラムの型の表示名。SSMS のオブジェクトエクスプローラーに合わせ、
/// 可変長の型には長さを、decimal 系には精度と位取りを添える。
/// </summary>
internal static class SqlServerTypeFormat
{
    private static readonly HashSet<string> ByteLengthTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "char", "varchar", "binary", "varbinary"
    };

    private static readonly HashSet<string> CharLengthTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "nchar", "nvarchar"
    };

    private static readonly HashSet<string> PrecisionScaleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "decimal", "numeric"
    };

    private static readonly HashSet<string> ScaleOnlyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "datetime2", "datetimeoffset", "time"
    };

    public static string Describe(string typeName, int maxLength, int precision, int scale)
    {
        if (ByteLengthTypes.Contains(typeName))
        {
            return $"{typeName}({LengthText(maxLength)})";
        }

        // nchar/nvarchar は 1 文字 2 バイトなので、max_length をそのまま出すと倍の長さに見える。
        if (CharLengthTypes.Contains(typeName))
        {
            return $"{typeName}({LengthText(maxLength == -1 ? -1 : maxLength / 2)})";
        }

        if (PrecisionScaleTypes.Contains(typeName))
        {
            return $"{typeName}({precision.ToString(CultureInfo.InvariantCulture)}, {scale.ToString(CultureInfo.InvariantCulture)})";
        }

        if (ScaleOnlyTypes.Contains(typeName))
        {
            return $"{typeName}({scale.ToString(CultureInfo.InvariantCulture)})";
        }

        return typeName;
    }

    private static string LengthText(int length) =>
        length == -1 ? "max" : length.ToString(CultureInfo.InvariantCulture);
}
