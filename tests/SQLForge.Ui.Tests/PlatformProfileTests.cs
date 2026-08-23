using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Linux;
using SQLForge.Infrastructure.MacOs;
using SQLForge.Infrastructure.Platform;
using SQLForge.Infrastructure.Windows;
using SQLForge.Ui.Composition;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// OS ごとの体裁。実装は OS ごとの別プロジェクトにあるが、選び分けは台帳ごしなので
/// どの OS の上からでも 3 つとも確かめられる（CI は Linux でしか回らないため）。
/// </summary>
public class PlatformProfileTests
{
    private static readonly PlatformProfileRegistry Registry = new(
    [
        new LinuxPlatformProfile(),
        new WindowsPlatformProfile(),
        new MacOsPlatformProfile()
    ]);

    [Theory]
    [InlineData(PlatformKind.Linux, typeof(LinuxPlatformProfile))]
    [InlineData(PlatformKind.Windows, typeof(WindowsPlatformProfile))]
    [InlineData(PlatformKind.MacOs, typeof(MacOsPlatformProfile))]
    public void OSごとの体裁を引き分ける(PlatformKind kind, Type expected)
    {
        var profile = Registry.ForHost(kind);

        Assert.IsType(expected, profile);
        Assert.Equal(kind, profile.Kind);
    }

    [Fact]
    public void 見分けのつかないOSでも既定の体裁で動く()
    {
        // 起動できなくなるより、体裁の既定のまま立ち上がるほうを選んである。
        var profile = Registry.ForHost(PlatformKind.Unknown);

        Assert.IsType<UnknownPlatformProfile>(profile);
        Assert.False(profile.PrefersNativeTitleBar);
    }

    [Fact]
    public void 自前のタイトルバーを使わないのはmacOSだけ()
    {
        Assert.True(Registry.ForHost(PlatformKind.MacOs).PrefersNativeTitleBar);
        Assert.False(Registry.ForHost(PlatformKind.Linux).PrefersNativeTitleBar);
        Assert.False(Registry.ForHost(PlatformKind.Windows).PrefersNativeTitleBar);
    }

    [Fact]
    public void 表示系の名前はOSごとに変わる()
    {
        Assert.Equal("Win32", Registry.ForHost(PlatformKind.Windows).DisplayServerName);
        Assert.Equal("Cocoa", Registry.ForHost(PlatformKind.MacOs).DisplayServerName);

        // Linux は同じ OS の中で X11 と Wayland に分かれる（実行中の環境で決まる）。
        var linux = Registry.ForHost(PlatformKind.Linux).DisplayServerName;
        Assert.True(linux is "X11" or "XWayland", linux);
    }

    [Fact]
    public void 合成ルートは実行中のOSの体裁を選ぶ()
    {
        Assert.Equal(HostPlatform.Current, PlatformProfiles.ForCurrentHost().Kind);
    }
}
