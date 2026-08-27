using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>接続情報の保存。パスワードは接続情報とは別に、キーリングへ預ける。</summary>
public sealed class SaveConnectionUseCase(IConnectionProfileRepository repository, ISecretStore secretStore)
{
    private readonly IConnectionProfileRepository _repository = repository;
    private readonly ISecretStore _secretStore = secretStore;

    public async Task<ConnectionValidationResult> ExecuteAsync(
        ConnectionDraft draft,
        ConnectionSecrets? secrets = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ConnectionValidator.Validate(draft);
        if (!validation.IsValid)
        {
            return validation;
        }

        var typed = secrets ?? ConnectionSecrets.None;
        var profile = draft.ToProfile();
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        await PersistSecretsAsync(profile, typed, cancellationToken).ConfigureAwait(false);

        return validation;
    }

    /// <summary>
    /// 資格情報を預ける、または預けてあるものを消す。DB のパスワードと踏み台のぶんを
    /// それぞれ別の鍵で扱う（踏み台をやめた接続に、古いパスワードを残さないため）。
    /// </summary>
    private async Task PersistSecretsAsync(
        ConnectionProfile profile,
        ConnectionSecrets secrets,
        CancellationToken cancellationToken)
    {
        if (!_secretStore.IsAvailable)
        {
            return;
        }

        await PersistAsync(
            SecretKeyFor(profile),
            profile.Credentials.StoreSecretInKeyring && profile.Credentials.RequiresSecret,
            secrets.Password,
            cancellationToken).ConfigureAwait(false);

        await PersistAsync(
            SshSecretKeyFor(profile),
            profile.Tunnel.StoreSecretInKeyring && profile.Tunnel.IsEnabled,
            secrets.SshSecret,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 鍵 1 つぶんの預け直し。
    /// 預けない設定になっていれば消し、入力欄が空のときは伏せて表示しているだけの
    /// 可能性があるので既存を残す。
    /// </summary>
    private async Task PersistAsync(
        string key,
        bool wanted,
        string? secret,
        CancellationToken cancellationToken)
    {
        if (!wanted)
        {
            await _secretStore.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrEmpty(secret))
        {
            await _secretStore.SaveAsync(key, secret, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string SecretKeyFor(ConnectionProfile profile) =>
        SecretKeyFor((profile ?? throw new ArgumentNullException(nameof(profile))).Id);

    /// <summary>接続そのものが手元に無いとき（削除したあとの後始末など）に使う。</summary>
    public static string SecretKeyFor(ConnectionProfileId id) => $"sqlforge:{id}";

    /// <summary>踏み台のパスワード（またはパスフレーズ）の預け先。DB のぶんとは別の鍵にする。</summary>
    public static string SshSecretKeyFor(ConnectionProfile profile) =>
        SshSecretKeyFor((profile ?? throw new ArgumentNullException(nameof(profile))).Id);

    public static string SshSecretKeyFor(ConnectionProfileId id) => $"sqlforge:{id}:ssh";
}
