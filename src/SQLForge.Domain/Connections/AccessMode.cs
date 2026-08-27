namespace SQLForge.Domain.Connections;

/// <summary>接続のアクセス種別。</summary>
public enum AccessMode
{
    /// <summary>
    /// 読み取りだけのつもりで開く接続。一覧とステータスバーに印を出すためのもので、
    /// 書き込みそのものは止めない。文面から書き込みかどうかを見分ける仕掛けは
    /// 動的 SQL のような形をどうせ通してしまうので、止めるのはサーバー側の権限の仕事にしてある。
    /// </summary>
    ReadOnly,

    /// <summary>書き込みを許可する。本番接続では明示的な昇格が必要。</summary>
    ReadWrite
}

/// <summary>認証方式。</summary>
public enum AuthenticationMethod
{
    /// <summary>ユーザー名とパスワード。</summary>
    Password,

    /// <summary>OS 統合認証（SQL Server の Windows 認証、Kerberos など）。</summary>
    Integrated,

    /// <summary>クライアント証明書。</summary>
    Certificate
}

/// <summary>
/// TLS の要求レベル。接続 URL の sslmode に対応する。
/// SSMS の [暗号化] （Optional / Mandatory / Strict）と証明書を信頼するかの組み合わせを、
/// 1 本の並びに畳んである。
/// </summary>
public enum TlsMode
{
    /// <summary>使わない。クライアントからは必須にしない。</summary>
    Disabled,

    /// <summary>可能なら使う。クライアントからは必須にしない。</summary>
    Prefer,

    /// <summary>必須。証明書は検証しない（SSMS の Mandatory + サーバー証明書を信頼する）。</summary>
    Require,

    /// <summary>必須かつ証明書を検証する（SSMS の Mandatory + 信頼しない）。</summary>
    VerifyFull,

    /// <summary>
    /// 厳密（SSMS の Strict）。TDS 8.0 で最初から TLS を張り、証明書は必ず検証する。
    /// SQL Server 2022 / Azure SQL 以降で使える。
    /// </summary>
    Strict
}
