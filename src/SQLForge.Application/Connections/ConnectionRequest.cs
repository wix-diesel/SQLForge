using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 接続を開くのに要る一式。<see cref="ConnectionProfile"/> はパスワードを持たないので、
/// 資格情報はここで別に添える。
/// </summary>
/// <param name="Profile">接続先と認証方式。</param>
/// <param name="Secret">パスワード。パスワード認証以外では null。</param>
public sealed record ConnectionRequest(ConnectionProfile Profile, string? Secret)
{
    private readonly TimeSpan? _timeout;

    /// <summary>踏み台のパスワード、または秘密鍵のパスフレーズ。トンネルを使わなければ null。</summary>
    public string? SshSecret { get; init; }

    /// <summary>
    /// 開いてある SSH トンネル。トンネルを使わない接続では null。
    /// ここに入っているトンネルは、セッションが開いたらセッションが閉じる。
    /// </summary>
    public ISshTunnel? Tunnel { get; init; }

    /// <summary>
    /// 実際にドライバーが繋ぎに行く先。トンネルを通すときは踏み台ではなく、
    /// 手元に開いた待ち受け口になる。
    /// </summary>
    public ServerAddress Endpoint => Tunnel?.LocalEndpoint ?? Profile.Target.Address;

    /// <summary>
    /// 接続確立の待ち時間。応答のないホストで固まらないようにする。
    /// 既定は「詳細設定」タブの接続タイムアウト（SSMS と同じく 15 秒）。
    /// </summary>
    public TimeSpan Timeout
    {
        get => _timeout ?? TimeSpan.FromSeconds(Profile.Advanced.ConnectTimeoutSeconds);
        init => _timeout = value;
    }
}
