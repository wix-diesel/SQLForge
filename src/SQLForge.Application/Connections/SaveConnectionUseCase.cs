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
        await StoreSecretAsync(profile, secret, cancellationToken).ConfigureAwait(false);

        return validation;
    }

    private async Task StoreSecretAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        var shouldStore = profile.Credentials.StoreSecretInKeyring
            && profile.Credentials.RequiresSecret
            && _secretStore.IsAvailable
            && !string.IsNullOrEmpty(secret);

        if (shouldStore)
        {
            await _secretStore.SaveAsync(SecretKeyFor(profile), secret!, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string SecretKeyFor(ConnectionProfile profile) => $"sqlforge:{profile.Id}";
}
