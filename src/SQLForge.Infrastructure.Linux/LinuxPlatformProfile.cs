using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Platform;

namespace SQLForge.Infrastructure.Linux;

/// <summary>
/// Linux での体裁。ウィンドウ装飾はモックアップどおりの自前タイトルバーで、
/// 表示系だけが X11 と Wayland で変わる。
/// </summary>
public sealed class LinuxPlatformProfile : PlatformProfileBase
{
    public override PlatformKind Kind => PlatformKind.Linux;

    /// <summary>Wayland でも Avalonia は XWayland 越しに X11 で描くので、そう名乗る。</summary>
    public override string DisplayServerName => DetectDisplayServer();

    private static string DetectDisplayServer()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var hasWaylandSocket = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase) || hasWaylandSocket)
        {
            return "XWayland";
        }

        return "X11";
    }
}
