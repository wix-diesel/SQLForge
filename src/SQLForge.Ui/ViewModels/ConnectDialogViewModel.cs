using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 起動時に出る接続ダイアログ全体。左ペイン・入力欄・フッターをまとめ、
/// 各操作をユースケースへ渡す。
///
/// 「接続」が通ると開いたセッションを <see cref="ConnectionEstablished"/> で渡し、
/// 受け取った側（App）がメインウィンドウへ引き継ぐ。
/// </summary>
public sealed partial class ConnectDialogViewModel : ObservableObject
{
    private readonly TestConnectionUseCase _testConnection;
    private readonly SaveConnectionUseCase _saveConnection;
    private readonly OpenConnectionUseCase _openConnection;
    private readonly ISecretStore _secretStore;

    [ObservableProperty] private DialogTabViewModel _selectedTab;
    [ObservableProperty] private string _statusHeadline = string.Empty;
    [ObservableProperty] private string _statusDetail = string.Empty;
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isStatusError;
    [ObservableProperty] private bool _isBusy;

    public ConnectDialogViewModel(
        SavedConnectionsViewModel savedConnections,
        TestConnectionUseCase testConnection,
        SaveConnectionUseCase saveConnection,
        OpenConnectionUseCase openConnection,
        ISecretStore secretStore,
        IPlatformProfile platform,
        IConnectionFilePrompt? files = null)
    {
        SavedConnections = savedConnections;
        _testConnection = testConnection;
        _saveConnection = saveConnection;
        _openConnection = openConnection;
        _secretStore = secretStore;

        // OS 統合認証で名乗るアカウント名を出すために、入力欄も OS の体裁を知る必要がある。
        // ファイル選択（秘密鍵・サーバー証明書の「参照…」）は親ウィンドウを持っている側から借りる。
        Form = new ConnectionFormViewModel(platform, files);
        Tabs = CreateTabs();
        _selectedTab = Tabs[0];

        // タブの見出しに出す印は、そのタブの入力が変わるたびに付け直す。
        Form.Ssh.PropertyChanged += (_, _) => RefreshBadges();
        Form.Certificate.PropertyChanged += (_, _) => RefreshBadges();
        Form.Advanced.PropertyChanged += (_, _) => RefreshBadges();

        SavedConnections.ProfileSelected += (_, profile) => OnProfileSelected(profile);
        SavedConnections.ProfileActivated += (_, profile) => _ = ConnectStoredAsync(profile);
        SavedConnections.LoadFailed += (_, exception) =>
            SetStatus(false, "保存済み接続を読み込めません", exception.Message);
        SavedConnections.OperationCompleted += (_, outcome) =>
            SetStatus(outcome.Succeeded, outcome.Headline, outcome.Detail);
    }

    public string Title => "データベース接続";

    public SavedConnectionsViewModel SavedConnections { get; }

    public ConnectionFormViewModel Form { get; }

    public IReadOnlyList<DialogTabViewModel> Tabs { get; }

    /// <summary>保管先の名前をそのまま出す（資格情報マネージャー・キーチェーン・キーリング）。</summary>
    public string KeyringLabel =>
        _secretStore.IsAvailable ? $"{_secretStore.DisplayName}に保存" : _secretStore.DisplayName;

    public bool IsKeyringAvailable => _secretStore.IsAvailable;

    /// <summary>閉じる・キャンセルが押されたことをウィンドウへ伝える。</summary>
    public event EventHandler? CloseRequested;

    /// <summary>接続が開いたことを伝える。セッションの後始末は受け取った側の責任。</summary>
    public event EventHandler<IDatabaseSession>? ConnectionEstablished;

    /// <summary>起動直後の読み込み。呼び出し側が待たないので、例外はここで受け止める。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SavedConnections.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            SetStatus(false, "保存済み接続を読み込めません", exception.Message);
        }

        if (SavedConnections.SelectedItem is null)
        {
            Form.LoadDraft();
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            var result = await _testConnection.ExecuteAsync(Form.ToDraft(), TypedSecrets(), cancellationToken).ConfigureAwait(true);
            Form.Validation = ConnectionValidator.Validate(Form.ToDraft());
            SetStatus(result.Succeeded, result.Headline, result.Detail);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            var draft = Form.ToDraft();
            var validation = await _saveConnection.ExecuteAsync(draft, TypedSecrets(), cancellationToken).ConfigureAwait(true);
            Form.Validation = validation;

            if (validation.IsValid)
            {
                await SavedConnections.LoadAsync(cancellationToken).ConfigureAwait(true);
                SetStatus(true, "保存しました", $"{draft.Name} を保存済み接続に反映しました。");
                return;
            }

            SetStatus(false, "保存できません", validation.FirstError!);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            var result = await _openConnection.ExecuteAsync(Form.ToDraft(), TypedSecrets(), cancellationToken).ConfigureAwait(true);
            Form.Validation = result.Validation;
            SetStatus(result.Succeeded, result.Headline, result.Detail);

            if (result.Session is { } session)
            {
                ConnectionEstablished?.Invoke(this, session);
            }
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// 左ペインで押された保存済み接続をそのまま開く。
    /// 入力欄に秘密が打たれていればそれを使い、無ければキーリングに預けたものを使う。
    /// どちらも無ければ接続は試みず、入力を促す。
    ///
    /// 打たれているかどうかは DB のパスワードと踏み台のぶんの両方で見る
    /// （踏み台のパスワードだけを打った、という開き方も通るようにするため）。
    /// </summary>
    private Task ConnectStoredAsync(ConnectionProfile profile) =>
        RunAsync(async () =>
        {
            var typed = TypedSecrets();
            var result = string.IsNullOrEmpty(typed.Password) && string.IsNullOrEmpty(typed.SshSecret)
                ? await _openConnection.ExecuteStoredAsync(profile).ConfigureAwait(true)
                : await _openConnection.ExecuteAsync(Form.ToDraft(), typed).ConfigureAwait(true);

            Form.Validation = result.Validation;
            SetStatus(result.Succeeded, result.Headline, result.Detail);

            if (result.Session is { } session)
            {
                ConnectionEstablished?.Invoke(this, session);
            }
        });

    [RelayCommand]
    private void NewConnection()
    {
        SavedConnections.SelectedItem = null;
        Form.LoadDraft();
        ClearStatus();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnProfileSelected(ConnectionProfile profile)
    {
        Form.Load(profile);
        ClearStatus();
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ClearStatus();
        }
        catch (Exception exception)
        {
            // 差し替え先の実装（DB ドライバー・キーリング）が投げても、
            // ダイアログは開いたままにして理由を出す。
            SetStatus(false, "操作に失敗しました", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetStatus(bool succeeded, string headline, string detail)
    {
        StatusHeadline = headline;
        StatusDetail = detail;
        IsStatusError = !succeeded;
        HasStatus = true;
    }

    private void ClearStatus()
    {
        StatusHeadline = string.Empty;
        StatusDetail = string.Empty;
        IsStatusError = false;
        HasStatus = false;
    }

    /// <summary>入力欄に打たれている秘密。DB のぶんと踏み台のぶんを 1 つにまとめて渡す。</summary>
    private ConnectionSecrets TypedSecrets() => new(Form.Password, Form.Ssh.Secret);

    /// <summary>既定と違う指定をしているタブの見出しに印を出す。</summary>
    private void RefreshBadges()
    {
        foreach (var tab in Tabs)
        {
            tab.Badge = tab.Kind switch
            {
                ConnectionDialogTab.SshTunnel => Form.Ssh.Badge,
                ConnectionDialogTab.Tls => Form.Certificate.Badge,
                ConnectionDialogTab.Advanced => Form.Advanced.Badge,
                _ => string.Empty
            };
        }
    }

    private static IReadOnlyList<DialogTabViewModel> CreateTabs() =>
    [
        new(ConnectionDialogTab.General, "一般"),
        new(ConnectionDialogTab.SshTunnel, "SSH トンネル"),
        new(ConnectionDialogTab.Tls, "TLS / SSL"),
        new(ConnectionDialogTab.Advanced, "詳細設定")
    ];
}
