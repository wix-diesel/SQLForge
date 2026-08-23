using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 接続に使うパスワードを決める。入力欄に打たれた値を最優先し、
/// 空のとき（保存済み接続を開いた直後など）だけキーリングに預けたものを使う。
/// </summary>
public sealed class ConnectionSecretResolver(ISecretStore secretStore)
{
    private readonly ISecretStore _secretStore = secretStore;

    public async Task<ConnectionRequest> ResolveAsync(
        ConnectionProfile profile,
        string? typedSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ConnectionRequest(profile, await ResolveSecretAsync(profile, typedSecret, cancellationToken).ConfigureAwait(false));
    }

    private async Task<string?> ResolveSecretAsync(
        ConnectionProfile profile,
        string? typedSecret,
        CancellationToken cancellationToken)
    {
        if (!profile.Credentials.RequiresSecret)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(typedSecret))
        {
            return typedSecret;
        }

        if (!profile.Credentials.StoreSecretInKeyring || !_secretStore.IsAvailable)
        {
            return null;
        }

        var key = SaveConnectionUseCase.SecretKeyFor(profile);
        return await _secretStore.ReadAsync(key, cancellationToken).ConfigureAwait(false);
    }
}
