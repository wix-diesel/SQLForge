using SQLForge.Domain.Connections;

namespace SQLForge.Application.Abstractions;

/// <summary>
/// SSH の踏み台ごしの経路を用意するポート。実装は手元に待ち受け口を 1 つ開き、
/// そこへ来たものを踏み台から <see cref="SshTunnelRequest.Destination"/> へ流す。
/// </summary>
public interface ISshTunnelBroker
{
    /// <summary>
    /// トンネルを開く。開けなければ <see cref="SQLForge.Application.Connections.SshTunnelException"/> を投げる
    /// （成功に見せかけて DB の接続失敗として出さない）。
    /// </summary>
    Task<ISshTunnel> OpenAsync(SshTunnelRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 開いている SSH トンネル 1 本。閉じると手元の待ち受け口も踏み台への接続も閉じる。
/// 後始末は受け取った側の責任（セッションが開けばセッションと一緒に閉じる）。
/// </summary>
public interface ISshTunnel : IAsyncDisposable
{
    /// <summary>手元の待ち受け口。DB へはここへ繋ぎに行く。</summary>
    ServerAddress LocalEndpoint { get; }

    /// <summary>接続テストの結果などに出す 1 行（踏み台と、そのホスト鍵の指紋）。</summary>
    string Description { get; }
}

/// <summary>
/// トンネルを開くのに要る一式。<see cref="SshTunnelSettings"/> はパスワードを持たないので、
/// 資格情報はここで別に添える。
/// </summary>
/// <param name="Settings">踏み台の指定。</param>
/// <param name="Destination">踏み台から見た繋ぎ先。</param>
/// <param name="Secret">パスワード、または秘密鍵のパスフレーズ。要らなければ null。</param>
public sealed record SshTunnelRequest(SshTunnelSettings Settings, ServerAddress Destination, string? Secret)
{
    /// <summary>踏み台への接続を待つ時間。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
