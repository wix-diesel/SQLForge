using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Connections;

/// <summary>「接続をテスト」。入力を検証してからプローブを呼ぶ。</summary>
public sealed class TestConnectionUseCase(IConnectionProbe probe)
{
    private readonly IConnectionProbe _probe = probe;

    public async Task<ConnectionProbeResult> ExecuteAsync(
        ConnectionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ConnectionValidator.Validate(draft);
        if (!validation.IsValid)
        {
            return ConnectionProbeResult.Failure(validation.FirstError!);
        }

        return await _probe.ProbeAsync(draft.ToProfile(), cancellationToken).ConfigureAwait(false);
    }
}
