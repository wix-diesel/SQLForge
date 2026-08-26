using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Workspace;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続後のメインウィンドウ。左のオブジェクトエクスプローラーと、
/// 右の作業領域（クエリエディタと結果グリッド）をまとめる。
///
/// 開いているセッションはこのビューモデルが持ち、ウィンドウが閉じるときに閉じる。
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IAsyncDisposable, IConnectionLauncher
{
    private readonly IDatabaseSession _session;
    private readonly IPlatformProfile _platform;

    public MainWindowViewModel(
        IDatabaseSession session,
        IPlatformProfile platform,
        CatalogContext catalog,
        QueryEditorViewModel query,
        TableEditorViewModel tableEditor)
    {
        _session = session;
        _platform = platform;
        catalog.ConnectionLauncher = this;
        Explorer = new ObjectExplorerViewModel(catalog);
        Query = query;
        TableEditor = tableEditor;

        // どちらを出すかはこの 2 つの開閉で決まる。片方が開いたら、もう片方の
        // 出し分けも変わるので、まとめてここで受ける。
        Query.PropertyChanged += OnWorkspaceChanged;
        TableEditor.PropertyChanged += OnWorkspaceChanged;
    }

    public ObjectExplorerViewModel Explorer { get; }

    /// <summary>右の作業領域。ツリーから「クエリを実行」を選ぶまでは畳んである。</summary>
    public QueryEditorViewModel Query { get; }

    /// <summary>同じ作業領域に出す編集グリッド。「先頭 100 行を編集」で開く。</summary>
    public TableEditorViewModel TableEditor { get; }

    /// <summary>
    /// 作業領域に出すもの。編集グリッドを開いている間はそちらを前に出し、
    /// 閉じるとクエリエディタが（開いていれば）戻ってくる。
    /// </summary>
    public bool ShowTableEditor => TableEditor.IsOpen;

    public bool ShowQuery => Query.IsOpen && !TableEditor.IsOpen;

    /// <summary>どちらも開いていないとき。選んでいるものの概要だけを出す。</summary>
    public bool ShowPlaceholder => !Query.IsOpen && !TableEditor.IsOpen;

    public event EventHandler? CloseRequested;

    /// <summary>ツリーの「接続解除」から上がってくる合図。セッションを閉じて起動画面へ戻すのは呼び出し元の役目。</summary>
    public event EventHandler? DisconnectRequested;

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

    void IConnectionLauncher.Disconnect() => DisconnectRequested?.Invoke(this, EventArgs.Empty);

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(QueryEditorViewModel.IsOpen))
        {
            return;
        }

        OnPropertyChanged(nameof(ShowTableEditor));
        OnPropertyChanged(nameof(ShowQuery));
        OnPropertyChanged(nameof(ShowPlaceholder));
    }

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
