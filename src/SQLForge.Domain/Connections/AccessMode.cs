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

/// <summary>TLS の要求レベル。接続 URL の sslmode に対応する。</summary>
public enum TlsMode
{
    Disabled,
    Prefer,
    Require,
    VerifyFull
}
