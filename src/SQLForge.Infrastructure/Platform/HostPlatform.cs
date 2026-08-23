using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Platform;

/// <summary>
/// 実行中の OS を見分けるだけの窓口。ここが持つのは「どの OS か」という判定で、
/// OS ごとの振る舞いは各 OS のプロジェクトが持つ。
/// </summary>
public static class HostPlatform
{
    /// <summary>実行中の OS。プロセスの寿命の間は変わらないので一度だけ調べる。</summary>
    public static PlatformKind Current { get; } = Detect();

    private static PlatformKind Detect()
    {
        if (OperatingSystem.IsLinux())
        {
            return PlatformKind.Linux;
        }

        if (OperatingSystem.IsWindows())
        {
            return PlatformKind.Windows;
        }

        return OperatingSystem.IsMacOS() ? PlatformKind.MacOs : PlatformKind.Unknown;
    }
}
