using System.Text;

namespace SQLForge.Infrastructure.PostgreSql;

/// <summary>
/// 識別子の引用符付け。スキーマ名やテーブル名はパラメータにできず SQL 文へ
/// 直接書くしかないので、二重引用符で囲み、中の引用符を二重にして閉じられないようにする。
///
/// PostgreSQL は囲まない識別子を小文字へ畳むので、囲むこと自体に意味がある
/// （SQL Server の角括弧と違い、囲む・囲まないで指す相手が変わる）。
/// </summary>
internal static class PostgreSqlIdentifier
{
    /// <summary>PostgreSQL の識別子の上限（NAMEDATALEN - 1 = 63 バイト）。</summary>
    private const int MaxLength = 63;

    public static string Quote(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException("識別子は空にできません。", nameof(identifier));
        }

        // 上限は文字数ではなくバイト数で決まる（多バイトの名前は文字数より早く届く）。
        if (Encoding.UTF8.GetByteCount(identifier) > MaxLength)
        {
            throw new ArgumentException($"識別子が長すぎます（{MaxLength} バイトまで）。", nameof(identifier));
        }

        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
