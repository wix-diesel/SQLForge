using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Platform;

namespace SQLForge.Infrastructure.MacOs;

/// <summary>
/// macOS での体裁。信号機ボタンを OS 側が描くので、自前のタイトルバーは使わない。
/// </summary>
public sealed class MacOsPlatformProfile : PlatformProfileBase
{
    public override PlatformKind Kind => PlatformKind.MacOs;

    public override string DisplayServerName => "Cocoa";

    /// <summary>信号機ボタンの位置と挙動は OS 標準の装飾に任せる。</summary>
    public override bool PrefersNativeTitleBar => true;
}
