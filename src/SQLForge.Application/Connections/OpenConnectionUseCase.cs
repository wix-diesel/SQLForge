using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Connections;

/// <summary>接続を開く操作の結果。</summary>
public sealed record OpenConnectionResult(
    bool Succeeded,
    string Headline,
    string Detail,
    ConnectionValidationResult Validation)
{
    public static OpenConnectionResult Invalid(ConnectionValidationResult validation) =>
        new(false, "接続できません", validation.FirstError ?? "入力を確認してください。", validation);
}

/// <summary>
/// ダイアログの「接続」。入力を検証し、到達確認まで行う。
/// セッションを開いてメインウィンドウへ引き渡す部分は、DB ドライバーを入れる段階で足す。
/// </summary>
public sealed class OpenConnectionUseCase(IConnectionProbe probe)
{
    private readonly IConnectionProbe _probe = probe;

    public async Task<OpenConnectionResult> ExecuteAsync(
        ConnectionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ConnectionValidator.Validate(draft);
        if (!validation.IsValid)
        {
            return OpenConnectionResult.Invalid(validation);
        }

        var probeResult = await _probe.ProbeAsync(draft.ToProfile(), cancellationToken).ConfigureAwait(false);

        return new OpenConnectionResult(
            probeResult.Succeeded,
            probeResult.Succeeded ? "接続しました" : probeResult.Headline,
            probeResult.Succeeded ? $"{probeResult.Detail} · セッションの引き渡しは未実装" : probeResult.Detail,
            validation);
    }
}
