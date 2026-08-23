using System.Globalization;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// ADO.NET が返した値をグリッドに出す文字列へ写す。
///
/// 表示は必ず不変カルチャで作る。日付や小数の見え方が実行環境の地域設定で変わると、
/// 同じクエリの結果が人によって違って見えてしまうため。
/// </summary>
internal static class AdoValueText
{
    public static string? From(object? value) => value switch
    {
        null or DBNull => null,

        // 16 進で出す。バイト列をそのまま文字へ写すと読めないものが出る。
        byte[] bytes => "0x" + Convert.ToHexString(bytes),

        // 日時は ISO 風に揃える。ロケール依存の「08/23/2026」は桁が揃わず読みにくい。
        DateTime moment => moment.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        DateTimeOffset moment => moment.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
        TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),

        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    /// <summary>グリッドで右へ寄せる型か。</summary>
    public static bool IsNumeric(Type type) => Type.GetTypeCode(type) is
        TypeCode.Byte or TypeCode.SByte or
        TypeCode.Int16 or TypeCode.UInt16 or
        TypeCode.Int32 or TypeCode.UInt32 or
        TypeCode.Int64 or TypeCode.UInt64 or
        TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
}
