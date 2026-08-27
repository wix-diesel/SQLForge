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

/// <summary>TLS 要求レベルの選択肢。並びは要求の強い順に下る。</summary>
public sealed record TlsChoice(TlsMode Mode, string DisplayName)
{
    public static IReadOnlyList<TlsChoice> All { get; } =
    [
        new(TlsMode.Disabled, "使用しない"),
        new(TlsMode.Prefer, "可能なら使用"),
        new(TlsMode.Require, "必須"),
        new(TlsMode.VerifyFull, "必須 + 証明書検証"),
        new(TlsMode.Strict, "厳密 (TDS 8.0)")
    ];

    public static TlsChoice For(TlsMode mode) => All.First(choice => choice.Mode == mode);

    public override string ToString() => DisplayName;
}

/// <summary>踏み台へ名乗るときの方式の選択肢。</summary>
public sealed record SshAuthenticationChoice(SshAuthenticationMethod Method, string DisplayName)
{
    public static IReadOnlyList<SshAuthenticationChoice> All { get; } =
    [
        new(SshAuthenticationMethod.Password, "パスワード"),
        new(SshAuthenticationMethod.PrivateKey, "秘密鍵")
    ];

    public static SshAuthenticationChoice For(SshAuthenticationMethod method) =>
        All.First(choice => choice.Method == method);

    public override string ToString() => DisplayName;
}

/// <summary>ネットワーク プロトコルの選択肢。SSMS の [ネットワーク プロトコル] と同じ並び。</summary>
public sealed record NetworkProtocolChoice(NetworkProtocol Protocol, string DisplayName)
{
    public static IReadOnlyList<NetworkProtocolChoice> All { get; } =
    [
        new(NetworkProtocol.Default, "<既定値>"),
        new(NetworkProtocol.SharedMemory, "共有メモリ"),
        new(NetworkProtocol.TcpIp, "TCP/IP"),
        new(NetworkProtocol.NamedPipes, "名前付きパイプ")
    ];

    public static NetworkProtocolChoice For(NetworkProtocol protocol) =>
        All.First(choice => choice.Protocol == protocol);

    public override string ToString() => DisplayName;
}
