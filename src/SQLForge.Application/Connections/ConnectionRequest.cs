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
    /// <summary>接続確立の待ち時間。応答のないホストで固まらないようにする。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
