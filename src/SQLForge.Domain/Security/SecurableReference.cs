namespace SQLForge.Domain.Security;

/// <summary>
/// 権限を付ける相手 1 つ。SSMS の「セキュリティ保護可能なリソース」の 1 行にあたる。
///
/// 種類と名前の組み合わせは常に妥当である前提なので、作るときにしか決められない
/// （<c>with</c> で後から差し替えられると、コンストラクタの検査をすり抜けてしまう）。
/// </summary>
public sealed record SecurableReference
{
    /// <param name="kind">リソースの種類。</param>
    /// <param name="name">リソースの名前。サーバーそのものではサーバー名を入れる。</param>
    /// <param name="schema">スキーマ。スキーマで修飾しない種類では null。</param>
    public SecurableReference(SecurableKind kind, string name, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("リソースの名前は空にできません。", nameof(name));
        }

        // 修飾が要る種類でスキーマを欠くと、文面が [dbo] 決め打ちへ倒れて
        // 頼んだのとは別のテーブルに権限が付く。組み立てる前にここで止める。
        if (kind.IsSchemaQualified() && string.IsNullOrWhiteSpace(schema))
        {
            throw new ArgumentException($"{kind.DisplayName()} にはスキーマが要ります。", nameof(schema));
        }

        Kind = kind;
        Name = name;
        Schema = kind.IsSchemaQualified() ? schema : null;
    }

    public SecurableKind Kind { get; }

    public string Name { get; }

    /// <summary>スキーマ。スキーマで修飾しない種類では必ず null。</summary>
    public string? Schema { get; }

    /// <summary>一覧に出す名前。修飾が要る種類は schema.name の形にする。</summary>
    public string DisplayName => Schema is { } schema ? $"{schema}.{Name}" : Name;

    /// <summary>サーバーそのものを指す。名前にはサーバー名を入れておく（文面には出ない）。</summary>
    public static SecurableReference Server(string serverName) =>
        new(SecurableKind.Server, serverName);
}
