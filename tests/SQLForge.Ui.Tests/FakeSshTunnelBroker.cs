using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 踏み台を立てずに SSH トンネルの筋書きだけを追うための差し替え。
/// 開いた回数と閉じた回数を数えるので、「開いたら必ず閉じる」ことを確かめられる。
/// </summary>
internal sealed class FakeSshTunnelBroker(int localPort = 43317) : ISshTunnelBroker
{
    /// <summary>開こうとしたときに投げる理由。null なら開く。</summary>
    public string? FailWith { get; set; }

    public int OpenCount { get; private set; }

    public int ClosedCount { get; private set; }

    public SshTunnelRequest? LastRequest { get; private set; }

    public Task<ISshTunnel> OpenAsync(SshTunnelRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;

        if (FailWith is { } reason)
        {
            throw new SshTunnelException(reason);
        }

        OpenCount++;

        return Task.FromResult<ISshTunnel>(
            new FakeSshTunnel(new ServerAddress("127.0.0.1", localPort), request.Settings.Summary, () => ClosedCount++));
    }

    /// <summary>本物と同じく、閉じる声が 2 か所から掛かっても数えるのは 1 回きり。</summary>
    private sealed class FakeSshTunnel(ServerAddress endpoint, string description, Action onClosed) : ISshTunnel
    {
        private bool _closed;

        public ServerAddress LocalEndpoint { get; } = endpoint;

        public string Description { get; } = description;

        public ValueTask DisposeAsync()
        {
            if (!_closed)
            {
                _closed = true;
                onClosed();
            }

            return ValueTask.CompletedTask;
        }
    }
}
