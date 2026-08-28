using System.Globalization;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// 「フィルターの設定」で日付を読み書きする形。SSMS と同じく年月日だけを扱う。
/// 打ち方は地域の設定に振り回されないよう、書き方を 1 つに決めて示す。
/// </summary>
public static class ObjectFilterDates
{
    /// <summary>入力欄に出す形。</summary>
    public const string Pattern = "yyyy/MM/dd";

    /// <summary>読み取れる形。区切りは / でも - でもよく、月日の 0 は省いてよい。</summary>
    private static readonly string[] Accepted = ["yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d"];

    public static string Format(DateOnly date) => date.ToString(Pattern, CultureInfo.InvariantCulture);

    /// <summary>打たれた文字列を日付として読む。読めなければ false。</summary>
    public static bool TryParse(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(
            (text ?? string.Empty).Trim(),
            Accepted,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
}
