using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 接続に使うパスワードを決める。入力欄に打たれた値を最優先し、
/// 空のとき（保存済み接続を開いた直後など）だけキーリングに預けたものを使う。
///
/// SSH トンネルを通す接続では、踏み台のぶんも同じ決め方で別の鍵から読む。
/// </summary>
public sealed class ConnectionSecretResolver(ISecretStore secretStore)
{
    private readonly ISecretStore _secretStore = secretStore;

    public async Task<ConnectionRequest> ResolveAsync(
        ConnectionProfile profile,
        ConnectionSecrets? typed = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var secrets = typed ?? ConnectionSecrets.None;

        return new ConnectionRequest(
            profile,
            await ResolveSecretAsync(profile, secrets.Password, cancellationToken).ConfigureAwait(false))
        {
            SshSecret = await ResolveSshSecretAsync(profile, secrets.SshSecret, cancellationToken).ConfigureAwait(false)
        };
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

        return await ResolveAsync(
            typedSecret,
            profile.Credentials.StoreSecretInKeyring,
            SaveConnectionUseCase.SecretKeyFor(profile),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 踏み台のぶん。秘密鍵にパスフレーズが掛かっていないこともあるので、
    /// 「無い」ことは失敗にせず null のまま返す。
    /// </summary>
    private async Task<string?> ResolveSshSecretAsync(
        ConnectionProfile profile,
        string? typedSecret,
        CancellationToken cancellationToken)
    {
        if (!profile.Tunnel.IsEnabled)
        {
            return null;
        }

        return await ResolveAsync(
            typedSecret,
            profile.Tunnel.StoreSecretInKeyring,
            SaveConnectionUseCase.SshSecretKeyFor(profile),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ResolveAsync(
        string? typedSecret,
        bool storedInKeyring,
        string key,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(typedSecret))
        {
            return typedSecret;
        }

        if (!storedInKeyring || !_secretStore.IsAvailable)
        {
            return null;
        }

        return await _secretStore.ReadAsync(key, cancellationToken).ConfigureAwait(false);
    }
}
