using System.Globalization;
using SQLForge.Application.Editing;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 編集グリッドのセルの値を、表示用の文字列と SQL Server の値との間で行き来させる。
///
/// グリッドに出ているのは <see cref="Infrastructure.Connections.AdoValueText"/> が作った文字列なので、
/// 書き戻すときは同じ書式から型へ写す。文字列のままパラメータに載せると、
/// bit の「True」やバイナリの「0x…」のように暗黙変換が効かない型で崩れる。
///
/// 型名は基本型（sys.types の system_type_id 側）を渡す。別名型（sysname など）でも
/// 中身は基本型なので、扱いを分ける必要がない。
/// </summary>
internal static class SqlServerCellValue
{
    /// <summary>セルとして扱える型の分類。</summary>
    private enum Kind
    {
        /// <summary>グリッドで扱えない型（バイナリ・LOB・CLR 型）。読むだけにする。</summary>
        Unsupported,
        Text,
        Integer,
        Decimal,
        Float,
        Bit,
        Moment,
        MomentWithOffset,
        Time,
        Guid
    }

    private static readonly Dictionary<string, Kind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["char"] = Kind.Text,
        ["nchar"] = Kind.Text,
        ["varchar"] = Kind.Text,
        ["nvarchar"] = Kind.Text,
        ["sysname"] = Kind.Text,
        ["tinyint"] = Kind.Integer,
        ["smallint"] = Kind.Integer,
        ["int"] = Kind.Integer,
        ["bigint"] = Kind.Integer,
        ["decimal"] = Kind.Decimal,
        ["numeric"] = Kind.Decimal,
        ["money"] = Kind.Decimal,
        ["smallmoney"] = Kind.Decimal,
        ["float"] = Kind.Float,
        ["real"] = Kind.Float,
        ["bit"] = Kind.Bit,
        ["date"] = Kind.Moment,
        ["datetime"] = Kind.Moment,
        ["datetime2"] = Kind.Moment,
        ["smalldatetime"] = Kind.Moment,
        ["datetimeoffset"] = Kind.MomentWithOffset,
        ["time"] = Kind.Time,
        ["uniqueidentifier"] = Kind.Guid
    };

    /// <summary>
    /// グリッドから書き換えられる型か。
    ///
    /// バイナリ・xml・空間型・rowversion は SSMS の編集グリッドでも書き換えられない。
    /// text / ntext / image は非推奨のうえ等値比較もできないので同じ扱いにする。
    /// </summary>
    public static bool IsEditable(string baseTypeName) => KindOf(baseTypeName) != Kind.Unsupported;

    /// <summary>
    /// 行を特定する条件に使える型か。主キーが無いテーブルで、どの列を鍵の代わりにするかを決める。
    /// </summary>
    public static bool IsComparable(string baseTypeName) => KindOf(baseTypeName) != Kind.Unsupported;

    /// <summary>グリッドで右へ寄せる型か。</summary>
    public static bool IsNumeric(string baseTypeName) =>
        KindOf(baseTypeName) is Kind.Integer or Kind.Decimal or Kind.Float;

    /// <summary>文字列の型か。空欄の確定を空文字列と NULL のどちらにするかを分ける。</summary>
    public static bool IsText(string baseTypeName) => KindOf(baseTypeName) == Kind.Text;

    /// <summary>
    /// 表示用の文字列を、パラメータへ載せる値へ写す。null（SQL の NULL）はそのまま返す。
    /// 型に合わない文字列は、サーバーへ送る前にここで弾く。
    /// </summary>
    public static object? ToParameter(string baseTypeName, string displayType, string? text)
    {
        if (text is null)
        {
            return null;
        }

        var kind = KindOf(baseTypeName);
        var value = Parse(kind, text);

        return value ?? throw new TableEditRejectedException(
            kind == Kind.Unsupported
                ? $"{displayType} 型の列はグリッドから書き換えられません。"
                : $"「{text}」は {displayType} 型の値として読み取れません。");
    }

    private static object? Parse(Kind kind, string text) => kind switch
    {
        Kind.Text => text,
        Kind.Integer => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null,
        Kind.Decimal => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null,
        Kind.Float => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null,
        Kind.Bit => ParseBit(text),
        Kind.Moment => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
            ? moment
            : null,
        Kind.MomentWithOffset =>
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
                ? moment
                : null,
        Kind.Time => TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time) ? time : null,
        Kind.Guid => Guid.TryParse(text, out var id) ? id : null,
        _ => null
    };

    /// <summary>
    /// bit は「True / False」で表示される（ADO.NET の bool の文字列表現）。
    /// SSMS と同じく 1 / 0 での入力も通す。
    /// </summary>
    private static object? ParseBit(string text) => text.Trim() switch
    {
        "1" => true,
        "0" => false,
        var other when bool.TryParse(other, out var value) => value,
        _ => null
    };

    private static Kind KindOf(string baseTypeName) =>
        Kinds.TryGetValue(baseTypeName, out var kind) ? kind : Kind.Unsupported;
}
