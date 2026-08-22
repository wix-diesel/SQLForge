using SQLForge.Domain.Connections;

namespace SQLForge.Ui.Presentation;

/// <summary>ドロップダウンに並べる列挙値と、その表示名の組。</summary>
public sealed record AuthenticationChoice(AuthenticationMethod Method, string DisplayName)
{
    public static IReadOnlyList<AuthenticationChoice> All { get; } =
    [
        new(AuthenticationMethod.Password, "パスワード"),
        new(AuthenticationMethod.Integrated, "OS 統合認証"),
        new(AuthenticationMethod.Certificate, "クライアント証明書")
    ];

    public static AuthenticationChoice For(AuthenticationMethod method) =>
        All.First(choice => choice.Method == method);

    public override string ToString() => DisplayName;
}

/// <summary>TLS 要求レベルの選択肢。</summary>
public sealed record TlsChoice(TlsMode Mode, string DisplayName)
{
    public static IReadOnlyList<TlsChoice> All { get; } =
    [
        new(TlsMode.Disabled, "使用しない"),
        new(TlsMode.Prefer, "可能なら使用"),
        new(TlsMode.Require, "必須"),
        new(TlsMode.VerifyFull, "必須 + 証明書検証")
    ];

    public static TlsChoice For(TlsMode mode) => All.First(choice => choice.Mode == mode);

    public override string ToString() => DisplayName;
}
