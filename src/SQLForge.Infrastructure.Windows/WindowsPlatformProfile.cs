using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Platform;

namespace SQLForge.Infrastructure.Windows;

/// <summary>
/// Windows での体裁。ウィンドウ装飾はモックアップどおりの自前タイトルバーを使う。
/// </summary>
public sealed class WindowsPlatformProfile : PlatformProfileBase
{
    public override PlatformKind Kind => PlatformKind.Windows;

    public override string DisplayServerName => "Win32";
}
