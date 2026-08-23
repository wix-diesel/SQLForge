using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Platform;

/// <summary>
/// OS ごとの体裁の共通部分。OS 固有の違いだけを派生側で上書きする。
///
/// ドライバーの <c>AdoDatabaseSession</c> と同じ考え方で、共通の足回りはここに置き、
/// 実装は OS ごとの別プロジェクト（SQLForge.Infrastructure.&lt;OS&gt;）が持つ。
/// </summary>
public abstract class PlatformProfileBase : IPlatformProfile
{
    public abstract PlatformKind Kind { get; }

    public abstract string DisplayServerName { get; }

    /// <summary>既定はモックアップどおりの自前タイトルバー。OS 標準に任せる側が上書きする。</summary>
    public virtual bool PrefersNativeTitleBar => false;

    /// <summary>
    /// 接続情報 (TOML) を置くディレクトリ。既定は .NET が OS ごとに返す場所
    /// （Linux は <c>~/.config</c>、Windows は <c>%APPDATA%</c>）。
    /// </summary>
    public virtual string ProfileDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "sqlforge");
}
