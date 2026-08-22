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
        string? secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ConnectionValidator.Validate(draft);
        if (!validation.IsValid)
        {
            return validation;
        }

        var profile = draft.ToProfile();
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        await PersistSecretAsync(profile, secret, cancellationToken).ConfigureAwait(false);

        return validation;
    }

    /// <summary>
    /// 資格情報を預ける、または預けてあるものを消す。
    /// 「キーリングに保存」を外した接続とパスワード認証をやめた接続では、
    /// 古い資格情報が残らないように消す。
    /// パスワード欄が空のときは、伏せて表示しているだけの可能性があるので既存を残す。
    /// </summary>
    private async Task PersistSecretAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        if (!_secretStore.IsAvailable)
        {
            return;
        }

        var key = SecretKeyFor(profile);

        if (!profile.Credentials.StoreSecretInKeyring || !profile.Credentials.RequiresSecret)
        {
            await _secretStore.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrEmpty(secret))
        {
            await _secretStore.SaveAsync(key, secret, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string SecretKeyFor(ConnectionProfile profile) => $"sqlforge:{profile.Id}";
}
