using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Security;

/// <summary>
/// OS のキーリングへ資格情報を預ける実装の共通部分。
///
/// <c>PlatformProfileBase</c> と同じ考え方で、共通の足回りだけをここに置き、
/// 実際の預け先は OS ごとの別プロジェクト（SQLForge.Infrastructure.&lt;OS&gt;）が持つ。
/// キーリングを使えない環境（別の OS で動いている・道具が入っていない）では、
/// 何も預からず何も返さない — 呼び出し側はパスワードの都度入力へ落ちる。
/// </summary>
public abstract class PlatformSecretStore : ISecretStore
{
    /// <summary>この預け先が受け持つ OS。</summary>
    public abstract PlatformKind Kind { get; }

    public abstract bool IsAvailable { get; }

    /// <summary>使えないときは、保管先の名前ではなく使えないことを名乗る。</summary>
    public string DisplayName => IsAvailable ? KeyringName : UnavailableSecretStore.UnavailableName;

    /// <summary>この OS でのキーリングの呼び名（資格情報マネージャー・キーチェーンなど）。</summary>
    protected abstract string KeyringName { get; }

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(secret);

        return IsAvailable ? SaveCoreAsync(key, secret, cancellationToken) : Task.CompletedTask;
    }

    public Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return IsAvailable ? ReadCoreAsync(key, cancellationToken) : Task.FromResult<string?>(null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return IsAvailable ? DeleteCoreAsync(key, cancellationToken) : Task.CompletedTask;
    }

    protected abstract Task SaveCoreAsync(string key, string secret, CancellationToken cancellationToken);

    protected abstract Task<string?> ReadCoreAsync(string key, CancellationToken cancellationToken);

    /// <summary>預けてあるものを消す。無いキーを指定しても失敗させない。</summary>
    protected abstract Task DeleteCoreAsync(string key, CancellationToken cancellationToken);
}
