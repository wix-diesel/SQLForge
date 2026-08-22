namespace SQLForge.Domain.Connections;

/// <summary>接続のアクセス種別。</summary>
public enum AccessMode
{
    /// <summary>INSERT / UPDATE / DELETE / DDL をクライアント側でブロックする。</summary>
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
