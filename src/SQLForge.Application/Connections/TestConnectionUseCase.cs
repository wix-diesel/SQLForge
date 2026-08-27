using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Connections;

/// <summary>
/// 「接続をテスト」。入力を検証し、資格情報を解決し、要るならトンネルを開いてからプローブを呼ぶ。
/// テストで開いたトンネルはテストの中で閉じる（接続は残さない）。
/// </summary>
public sealed class TestConnectionUseCase(
    IConnectionProbe probe,
    ConnectionSecretResolver secrets,
    ConnectionTunnelOpener tunnels)
{
    private readonly IConnectionProbe _probe = probe;
    private readonly ConnectionSecretResolver _secrets = secrets;
    private readonly ConnectionTunnelOpener _tunnels = tunnels;

    public async Task<ConnectionProbeResult> ExecuteAsync(
        ConnectionDraft draft,
        ConnectionSecrets? typedSecrets = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ConnectionValidator.Validate(draft);
        if (!validation.IsValid)
        {
            return ConnectionProbeResult.Failure(validation.FirstError!);
        }

        var request = await _secrets.ResolveAsync(draft.ToProfile(), typedSecrets, cancellationToken).ConfigureAwait(false);

        ConnectionRequest tunneled;
        try
        {
            tunneled = await _tunnels.OpenAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (SshTunnelException exception)
        {
            return ConnectionProbeResult.Failure(exception.Message);
        }

        try
        {
            return await _probe.ProbeAsync(tunneled, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (tunneled.Tunnel is { } tunnel)
            {
                await tunnel.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
