using CommunityToolkit.Mvvm.ComponentModel;

namespace SQLForge.Ui.ViewModels;

/// <summary>接続ダイアログのタブ 1 枚ぶんの区別。どの入力欄を出すかを決める。</summary>
public enum ConnectionDialogTab
{
    General,
    SshTunnel,
    Tls,
    Advanced
}

/// <summary>
/// 接続ダイアログのタブ 1 枚。<see cref="Badge"/> は、そのタブで既定と違う指定を
/// しているときに見出しへ出す印（デザインの緑の点）。
/// </summary>
public sealed partial class DialogTabViewModel(ConnectionDialogTab kind, string title) : ObservableObject
{
    [ObservableProperty] private string _badge = string.Empty;

    public ConnectionDialogTab Kind { get; } = kind;

    public string Title { get; } = title;

    public bool HasBadge => Badge.Length > 0;

    public bool IsGeneral => Kind == ConnectionDialogTab.General;

    public bool IsSshTunnel => Kind == ConnectionDialogTab.SshTunnel;

    public bool IsTls => Kind == ConnectionDialogTab.Tls;

    public bool IsAdvanced => Kind == ConnectionDialogTab.Advanced;

    partial void OnBadgeChanged(string value) => OnPropertyChanged(nameof(HasBadge));
}
