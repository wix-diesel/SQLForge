using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Connections;

/// <summary>「接続をテスト」。入力を検証し、資格情報を解決してからプローブを呼ぶ。</summary>
public sealed class TestConnectionUseCase(IConnectionProbe probe, ConnectionSecretResolver secrets)
{
    private readonly IConnectionProbe _probe = probe;
    private readonly ConnectionSecretResolver _secrets = secrets;

    public async Task<ConnectionProbeResult> ExecuteAsync(
        ConnectionDraft draft,
        string? typedSecret = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ConnectionValidator.Validate(draft);
        if (!validation.IsValid)
        {
            return ConnectionProbeResult.Failure(validation.FirstError!);
        }

        var request = await _secrets.ResolveAsync(draft.ToProfile(), typedSecret, cancellationToken).ConfigureAwait(false);

        return await _probe.ProbeAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
