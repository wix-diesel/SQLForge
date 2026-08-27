namespace SQLForge.Application.Connections;

/// <summary>
/// 入力欄に打たれている秘密。SSH トンネルを通す接続では、DB のパスワードとは別に
/// 踏み台のパスワード（または秘密鍵のパスフレーズ）が要るので、2 つをまとめて渡す。
///
/// どちらも空のときは、キーリングに預けてあるものを使う
/// （解決の順番は <see cref="ConnectionSecretResolver"/>）。
/// </summary>
/// <param name="Password">DB のパスワード。</param>
/// <param name="SshSecret">踏み台のパスワード、または秘密鍵のパスフレーズ。</param>
public sealed record ConnectionSecrets(string? Password = null, string? SshSecret = null)
{
    /// <summary>入力欄に何も打たれていない状態。</summary>
    public static ConnectionSecrets None { get; } = new();
}
