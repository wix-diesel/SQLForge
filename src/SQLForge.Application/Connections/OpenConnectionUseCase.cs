using System.Data.Common;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

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
    /// <summary>パスワードが要るのに手元に無い、という失敗。入力を促すのに使う。</summary>
    public bool RequiresSecret { get; init; }

    public static OpenConnectionResult Invalid(ConnectionValidationResult validation) =>
        new(false, "接続できません", validation.FirstError ?? "入力を確認してください。", validation);

    public static OpenConnectionResult Failure(string detail) =>
        new(false, "接続に失敗", detail, ConnectionValidationResult.Valid);

    /// <summary>
    /// 保存済み接続を開こうとしたが、パスワードが預けられていなかったとき。
    /// 空のパスワードでサーバーを叩いて失敗を出すより、入力を促すほうが分かりやすい。
    /// </summary>
    public static OpenConnectionResult SecretRequired(ConnectionProfile profile) =>
        new(false,
            "パスワードが必要です",
            $"{profile.Name} のパスワードは預けられていません。入力して「接続」を押してください。",
            ConnectionValidationResult.Valid)
        {
            RequiresSecret = true
        };
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

        var request = await _secrets.ResolveAsync(draft.ToProfile(), typedSecret, cancellationToken).ConfigureAwait(false);

        return await OpenAsync(request, validation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 保存済み接続をそのまま開く（左ペインで選んだときの自動接続）。
    /// 入力欄を通らないので、パスワードはキーリングに預けてあるものだけを使う。
    /// </summary>
    public async Task<OpenConnectionResult> ExecuteStoredAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var validation = ConnectionValidator.Validate(ConnectionDraft.FromProfile(profile));
        if (!validation.IsValid)
        {
            return OpenConnectionResult.Invalid(validation);
        }

        var request = await _secrets.ResolveAsync(profile, null, cancellationToken).ConfigureAwait(false);
        if (profile.Credentials.RequiresSecret && string.IsNullOrEmpty(request.Secret))
        {
            return OpenConnectionResult.SecretRequired(profile);
        }

        return await OpenAsync(request, validation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OpenConnectionResult> OpenAsync(
        ConnectionRequest request,
        ConnectionValidationResult validation,
        CancellationToken cancellationToken)
    {
        var driver = request.Profile.Target.Driver;
        if (!_registry.TryResolve(driver, out var connector))
        {
            return OpenConnectionResult.Failure(UnsupportedDriverMessage.For(driver, _registry.SupportedDrivers));
        }

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
