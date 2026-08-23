using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Platform;

/// <summary>
/// 合成ルートに登録された体裁を OS 別に引けるようにする。
/// <c>DatabaseConnectorRegistry</c> と同じ考え方で、OS を増やすときは
/// <see cref="IPlatformProfile"/> の実装を渡す並びへ足すだけでよく、ここは触らない。
/// </summary>
public sealed class PlatformProfileRegistry
{
    private static readonly UnknownPlatformProfile Fallback = new();

    private readonly Dictionary<PlatformKind, IPlatformProfile> _profiles;

    public PlatformProfileRegistry(IEnumerable<IPlatformProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        _profiles = profiles.ToDictionary(profile => profile.Kind);
    }

    /// <summary>実行中の OS に合う体裁。</summary>
    public IPlatformProfile ForCurrentHost() => ForHost(HostPlatform.Current);

    /// <summary>
    /// 指定した OS に合う体裁。名乗り出るものが無ければ
    /// <see cref="UnknownPlatformProfile"/> を返す（起動できなくなるより既定で動かす）。
    /// </summary>
    public IPlatformProfile ForHost(PlatformKind kind) =>
        _profiles.TryGetValue(kind, out var profile) ? profile : Fallback;
}
