namespace SQLForge.Application.Abstractions;

/// <summary>動作中の OS 種別。初期版は Linux だが、Windows / macOS も想定しておく。</summary>
public enum PlatformKind
{
    Linux,
    Windows,
    MacOs,
    Unknown
}

/// <summary>OS ごとに変わる事柄をまとめたポート。UI はこれを見て体裁を切り替える。</summary>
public interface IPlatformProfile
{
    PlatformKind Kind { get; }

    /// <summary>OS 標準のタイトルバーを使うか。macOS は信号機ボタンがあるので true。</summary>
    bool PrefersNativeTitleBar { get; }

    /// <summary>ステータス表示に出す表示系の名前（X11 / Wayland / Win32 / Cocoa）。</summary>
    string DisplayServerName { get; }

    /// <summary>接続情報 (TOML) を置くディレクトリ。</summary>
    string ProfileDirectory { get; }
}
