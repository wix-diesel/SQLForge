using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Platform;

/// <summary>
/// どの OS のプロジェクトも名乗り出なかったときの受け皿。
/// 見分けがつかない OS でも起動だけはできるように、既定の体裁をそのまま使う。
/// </summary>
public sealed class UnknownPlatformProfile : PlatformProfileBase
{
    public override PlatformKind Kind => PlatformKind.Unknown;

    public override string DisplayServerName => "不明";
}
