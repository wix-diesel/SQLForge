using Renci.SshNet;
using Renci.SshNet.Common;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 開いている SSH トンネル 1 本。閉じる順は「手元の待ち受け口 → 踏み台への接続」で、
/// 先に踏み台を切ると、待ち受け口に来た接続が行き先を失う。
/// </summary>
internal sealed class SshTunnel(
    SshClient client,
    ForwardedPortLocal forwarded,
    ServerAddress localEndpoint,
    string description) : ISshTunnel
{
    private bool _closed;

    public ServerAddress LocalEndpoint { get; } = localEndpoint;

    public string Description { get; } = description;

    public ValueTask DisposeAsync()
    {
        // 閉じる声は 2 か所から掛かる。接続テストは自分で開けたぶんをその場で閉じ、
        // セッションが開けていればセッションも閉じにくる。どちらが先でも同じになるよう、
        // 実際に閉じるのは最初の 1 回だけにする。
        if (_closed)
        {
            return ValueTask.CompletedTask;
        }

        _closed = true;

        try
        {
            if (forwarded.IsStarted)
            {
                forwarded.Stop();
            }
        }
        catch (SshException)
        {
            // 踏み台側が先に切れていた場合。閉じ切ることだけが目的なので、理由は問わない。
        }

        forwarded.Dispose();
        client.Dispose();

        return ValueTask.CompletedTask;
    }
}
