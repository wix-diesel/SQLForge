using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;
using SQLForge.Ui.ViewModels.Explorer;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続後のメインウィンドウ。今あるのはオブジェクトエクスプローラーだけで、
/// 作業領域（クエリエディタと結果グリッド）はフェーズ 1 の残り。
///
/// 開いているセッションはこのビューモデルが持ち、ウィンドウが閉じるときに閉じる。
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDatabaseSession _session;
    private readonly IPlatformProfile _platform;

    public MainWindowViewModel(IDatabaseSession session, IPlatformProfile platform, CatalogContext catalog)
    {
        _session = session;
        _platform = platform;
        Explorer = new ObjectExplorerViewModel(catalog);
    }

    public ObjectExplorerViewModel Explorer { get; }

    public event EventHandler? CloseRequested;

    private ConnectionProfile Profile => _session.Profile;

    public string ConnectionName => Profile.Name;

    /// <summary>タイトルバーに出す文字列。環境タグを必ず添える。</summary>
    public string Title => $"SQLForge — {Profile.Name} · {Profile.Environment.DisplayName}";

    public string EnvironmentName => Profile.Environment.DisplayName;

    public bool IsCritical => Profile.Environment.Severity == EnvironmentSeverity.Critical;

    public bool IsElevated => Profile.Environment.Severity == EnvironmentSeverity.Elevated;

    public bool IsNormal => Profile.Environment.Severity == EnvironmentSeverity.Normal;

    public bool IsNeutral => Profile.Environment.Severity == EnvironmentSeverity.Neutral;

    public bool IsReadOnly => Profile.IsReadOnly;

    public string ServerDescription => _session.Server.Description;

    /// <summary>ステータスバー右端。例: dbo · 10.2.0.14:1433 · X11</summary>
    public string TargetSummary => $"{Profile.Target.Address} · {_platform.DisplayServerName}";

    public string DatabaseName => Profile.Target.Database;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Explorer.InitializeAsync(cancellationToken);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
