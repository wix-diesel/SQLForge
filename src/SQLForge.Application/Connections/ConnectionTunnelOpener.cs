using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Connections;

/// <summary>
/// SSH トンネルが要る接続なら、DB へ繋ぐ前にトンネルを開く。
///
/// 開いたトンネルは接続要求に添えて返すだけで、閉じるのは受け取った側の責任にしてある。
/// セッションが開けばセッションと一緒に閉じ（<see cref="ConnectionRequest.Tunnel"/> を
/// ドライバーがセッションへ預ける）、開かなければ呼び出し側がその場で閉じる。
/// </summary>
public sealed class ConnectionTunnelOpener(ISshTunnelBroker broker)
{
    private readonly ISshTunnelBroker _broker = broker;

    /// <summary>
    /// 必要ならトンネルを開き、経路を差し込んだ接続要求を返す。
    /// トンネルを使わない接続では、渡されたものをそのまま返す。
    /// </summary>
    public async Task<ConnectionRequest> OpenAsync(
        ConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tunnel = request.Profile.Tunnel;
        if (!tunnel.IsEnabled)
        {
            return request;
        }

        var opened = await _broker.OpenAsync(
            new SshTunnelRequest(tunnel, request.Profile.Target.Address, request.SshSecret)
            {
                Timeout = request.Timeout
            },
            cancellationToken).ConfigureAwait(false);

        return request with { Tunnel = opened };
    }
}
