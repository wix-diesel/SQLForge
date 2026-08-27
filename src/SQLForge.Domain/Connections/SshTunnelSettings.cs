namespace SQLForge.Domain.Connections;

/// <summary>踏み台へ名乗るときの方式。</summary>
public enum SshAuthenticationMethod
{
    /// <summary>パスワード。</summary>
    Password,

    /// <summary>秘密鍵。鍵にパスフレーズが掛かっていればそれを添える。</summary>
    PrivateKey
}

/// <summary>
/// SSH の踏み台ごしに繋ぐための指定。手元に開けた待ち受け口から踏み台を通し、
/// 踏み台から見た繋ぎ先（<see cref="ConnectionTarget"/> のアドレス）へ流す。
///
/// パスワードとパスフレーズそのものは持たない（<see cref="ConnectionCredentials"/> と同じく、
/// 預け先は OS のキーリング）。
/// </summary>
public sealed record SshTunnelSettings
{
    /// <summary>SSH の既定ポート。</summary>
    public const int DefaultPort = 22;

    private readonly string _host = string.Empty;
    private readonly string _userName = string.Empty;
    private readonly string _privateKeyPath = string.Empty;

    /// <summary>トンネルを使わない状態。</summary>
    public static SshTunnelSettings Disabled { get; } = new();

    /// <summary>トンネルを通すか。切っていれば他の欄は使わない。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>踏み台のホスト。</summary>
    public string Host
    {
        get => _host;
        init => _host = Clean(value);
    }

    /// <summary>踏み台のポート。</summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>踏み台へ名乗る利用者名。</summary>
    public string UserName
    {
        get => _userName;
        init => _userName = Clean(value);
    }

    public SshAuthenticationMethod Authentication { get; init; }

    /// <summary>秘密鍵のファイル。先頭の <c>~</c> はホームとして扱う。</summary>
    public string PrivateKeyPath
    {
        get => _privateKeyPath;
        init => _privateKeyPath = Clean(value);
    }

    /// <summary>手元に開ける待ち受けポート。0 は空いているポートを自動で選ぶ。</summary>
    public int LocalPort { get; init; }

    /// <summary>パスワード（またはパスフレーズ）を OS のキーリングへ預けるか。</summary>
    public bool StoreSecretInKeyring { get; init; } = true;

    /// <summary>手元の待ち受けポートを自動で選ぶ状態。</summary>
    public bool UsesAutomaticLocalPort => LocalPort == 0;

    /// <summary>パスワードが無いと繋げない状態。秘密鍵のパスフレーズは無くてもよいので含めない。</summary>
    public bool RequiresSecret => IsEnabled && Authentication == SshAuthenticationMethod.Password;

    /// <summary>秘密鍵のファイルが要る状態。</summary>
    public bool RequiresPrivateKey => IsEnabled && Authentication == SshAuthenticationMethod.PrivateKey;

    /// <summary>接続テストの結果などに出す 1 行（例: alice@bastion.internal:22）。</summary>
    public string Summary =>
        UserName.Length > 0 ? $"{UserName}@{Host}:{Port}" : $"{Host}:{Port}";

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
}
