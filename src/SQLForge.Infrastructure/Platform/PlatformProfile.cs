using System.Runtime.InteropServices;
using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Platform;

/// <summary>
/// 実行中の OS を見て体裁の既定を決める。初期版の対象は Linux だが、
/// Windows / macOS でもそのまま起動できるようにしてある。
/// </summary>
public sealed class PlatformProfile : IPlatformProfile
{
    public PlatformKind Kind { get; } = Detect();

    /// <summary>macOS は信号機ボタンを OS 側が描くので、自前のタイトルバーを使わない。</summary>
    public bool PrefersNativeTitleBar => Kind == PlatformKind.MacOs;

    public string DisplayServerName => Kind switch
    {
        PlatformKind.Linux => DetectLinuxDisplayServer(),
        PlatformKind.Windows => "Win32",
        PlatformKind.MacOs => "Cocoa",
        _ => "不明"
    };

    public string ProfileDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "sqlforge");

    private static PlatformKind Detect()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return PlatformKind.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return PlatformKind.Windows;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? PlatformKind.MacOs : PlatformKind.Unknown;
    }

    private static string DetectLinuxDisplayServer()
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
