using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Platform;

namespace SQLForge.Infrastructure.Security;

/// <summary>
/// 合成ルートに登録された預け先を OS 別に引けるようにする。
/// <c>PlatformProfileRegistry</c> と同じ考え方で、OS を増やすときは
/// <see cref="PlatformSecretStore"/> の実装を渡す並びへ足すだけでよく、ここは触らない。
/// </summary>
public sealed class SecretStoreRegistry
{
    private static readonly UnavailableSecretStore Fallback = new();

    private readonly Dictionary<PlatformKind, PlatformSecretStore> _stores;

    public SecretStoreRegistry(IEnumerable<PlatformSecretStore> stores)
    {
        ArgumentNullException.ThrowIfNull(stores);

        _stores = stores.ToDictionary(store => store.Kind);
    }

    /// <summary>実行中の OS の預け先。</summary>
    public ISecretStore ForCurrentHost() => ForHost(HostPlatform.Current);

    /// <summary>
    /// 指定した OS の預け先。名乗り出るものが無ければ
    /// <see cref="UnavailableSecretStore"/> を返す（キーリング無しでも起動はできる）。
    /// </summary>
    public ISecretStore ForHost(PlatformKind kind) =>
        _stores.TryGetValue(kind, out var store) ? store : Fallback;
}
