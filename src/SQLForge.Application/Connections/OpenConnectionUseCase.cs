using System.Data.Common;
using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Connections;

/// <summary>
/// 接続を開く操作の結果。成功したときだけ <see cref="Session"/> が入る。
/// セッションの後始末は受け取った側（メインウィンドウ）の責任。
/// </summary>
public sealed record OpenConnectionResult(
    bool Succeeded,
    string Headline,
    string Detail,
    ConnectionValidationResult Validation,
    IDatabaseSession? Session = null)
{
    public static OpenConnectionResult Invalid(ConnectionValidationResult validation) =>
        new(false, "接続できません", validation.FirstError ?? "入力を確認してください。", validation);

    public static OpenConnectionResult Failure(string detail) =>
        new(false, "接続に失敗", detail, ConnectionValidationResult.Valid);
}

/// <summary>
/// ダイアログの「接続」。入力を検証し、資格情報を解決し、ドライバーでセッションを開く。
/// 開いたセッションはそのまま呼び出し側へ渡す（メインウィンドウが受け取って使い、閉じる）。
/// </summary>
public sealed class OpenConnectionUseCase(IDatabaseConnectorRegistry registry, ConnectionSecretResolver secrets)
{
    private readonly IDatabaseConnectorRegistry _registry = registry;
    private readonly ConnectionSecretResolver _secrets = secrets;

    public async Task<OpenConnectionResult> ExecuteAsync(
        ConnectionDraft draft,
        string? typedSecret = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ConnectionValidator.Validate(draft);
        if (!validation.IsValid)
        {
            return OpenConnectionResult.Invalid(validation);
        }

        if (!_registry.TryResolve(draft.Driver, out var connector))
        {
            return OpenConnectionResult.Failure(UnsupportedDriverMessage.For(draft.Driver, _registry.SupportedDrivers));
        }

        var request = await _secrets.ResolveAsync(draft.ToProfile(), typedSecret, cancellationToken).ConfigureAwait(false);

        try
        {
            var session = await connector.ConnectAsync(request, cancellationToken).ConfigureAwait(false);

            return new OpenConnectionResult(
                true,
                "接続しました",
                $"{session.Server.Description} · {request.Profile.Target.Database}",
                validation,
                session);
        }
        catch (DbException exception)
        {
            return OpenConnectionResult.Failure(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            // 接続文字列を組む段でドライバーが受け付けないと分かったもの（証明書認証など）。
            return OpenConnectionResult.Failure(exception.Message);
        }
    }
}
