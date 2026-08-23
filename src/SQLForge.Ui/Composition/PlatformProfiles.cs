using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Linux;
using SQLForge.Infrastructure.MacOs;
using SQLForge.Infrastructure.Platform;
using SQLForge.Infrastructure.Windows;

namespace SQLForge.Ui.Composition;

/// <summary>
/// OS ごとの体裁の実装を知ってよいのは合成ルートだけ。
/// OS を増やすときに触るのは、新しい SQLForge.Infrastructure.&lt;OS&gt; と、この並びの 1 行。
/// </summary>
public static class PlatformProfiles
{
    /// <summary>体裁そのものは状態を持たないので、台帳ごと使い回す。</summary>
    private static readonly Lazy<PlatformProfileRegistry> Registry = new(() => new PlatformProfileRegistry(
    [
        new LinuxPlatformProfile(),
        new WindowsPlatformProfile(),
        new MacOsPlatformProfile()
    ]));

    /// <summary>実行中の OS に合う体裁を 1 つ選ぶ。どれも名乗り出なければ既定の体裁になる。</summary>
    public static IPlatformProfile ForCurrentHost() => Registry.Value.ForCurrentHost();
}
