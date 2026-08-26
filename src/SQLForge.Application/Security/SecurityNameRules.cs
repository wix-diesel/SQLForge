namespace SQLForge.Application.Security;

/// <summary>
/// 識別子の入力欄に共通の検査。ユーザー名・ロール名・スキーマ名・所有者はどれも
/// SQL 文へ直接埋め込むので、「空でない・制御文字を含まない・長すぎない」の 3 つを
/// サーバーへ送る前に見る、という形が同じになる。
/// </summary>
internal static class SecurityNameRules
{
    /// <summary>SQL Server の識別子の上限（sysname = nvarchar(128)）。</summary>
    public const int MaxLength = 128;

    /// <summary>
    /// 必ず要る名前を見る。<paramref name="label"/> はそのままメッセージに出す見出し
    /// （「ロール名」「スキーマ名」など）。
    /// </summary>
    /// <returns>直すべきところがあればその理由、無ければ null。</returns>
    public static string? Required(string value, string label)
    {
        var trimmed = value.Trim();

        return trimmed.Length == 0 ? $"{label}を入力してください。" : Optional(trimmed, label);
    }

    /// <summary>未指定を許す名前を見る。空なら何も言わない。</summary>
    /// <returns>直すべきところがあればその理由、無ければ null。</returns>
    public static string? Optional(string value, string label)
    {
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Any(char.IsControl))
        {
            return $"{label}に制御文字は使えません。";
        }

        return trimmed.Length > MaxLength ? $"{label}は {MaxLength} 文字までです。" : null;
    }

    /// <summary>
    /// 同じ名前の集まりか。並び順は見ない（チェックボックスの並び替えを変更とは呼ばない）。
    /// </summary>
    public static bool SameSet(IEnumerable<string> left, IEnumerable<string> right) =>
        new HashSet<string>(left, StringComparer.OrdinalIgnoreCase)
            .SetEquals(new HashSet<string>(right, StringComparer.OrdinalIgnoreCase));

    /// <summary>理由があるときだけ欄へ書き込む。すでに理由がある欄は上書きしない。</summary>
    public static void Set(IDictionary<string, string> errors, string field, string? message)
    {
        if (message is not null && !errors.ContainsKey(field))
        {
            errors[field] = message;
        }
    }
}
