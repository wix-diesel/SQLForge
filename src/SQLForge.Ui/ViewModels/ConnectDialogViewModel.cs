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
        ISecretStore secretStore)
    {
        SavedConnections = savedConnections;
        _testConnection = testConnection;
        _saveConnection = saveConnection;
        _openConnection = openConnection;
        _secretStore = secretStore;

        Form = new ConnectionFormViewModel();
        Tabs = CreateTabs();
        _selectedTab = Tabs[0];

        SavedConnections.ProfileSelected += (_, profile) => OnProfileSelected(profile);
        SavedConnections.ProfileActivated += (_, profile) => _ = ConnectStoredAsync(profile);
        SavedConnections.LoadFailed += (_, exception) =>
            SetStatus(false, "保存済み接続を読み込めません", exception.Message);
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
            var result = await _testConnection.ExecuteAsync(Form.ToDraft(), Form.Password, cancellationToken).ConfigureAwait(true);
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
            var validation = await _saveConnection.ExecuteAsync(draft, Form.Password, cancellationToken).ConfigureAwait(true);
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
            var result = await _openConnection.ExecuteAsync(Form.ToDraft(), Form.Password, cancellationToken).ConfigureAwait(true);
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
    /// 入力欄にパスワードが打たれていればそれを使い、無ければキーリングに預けたものを使う。
    /// どちらも無ければ接続は試みず、入力を促す。
    /// </summary>
    private Task ConnectStoredAsync(ConnectionProfile profile) =>
        RunAsync(async () =>
        {
            var result = string.IsNullOrEmpty(Form.Password)
                ? await _openConnection.ExecuteStoredAsync(profile).ConfigureAwait(true)
                : await _openConnection.ExecuteAsync(Form.ToDraft(), Form.Password).ConfigureAwait(true);

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

    private static IReadOnlyList<DialogTabViewModel> CreateTabs() =>
    [
        new("一般", true),
        new("SSH トンネル", false, "SSH 経由の接続はフェーズ 1 の後半で対応します。"),
        new("TLS / SSL", false, "証明書の指定はフェーズ 1 の後半で対応します。現状は「一般」タブの TLS 設定のみ有効です。"),
        new("詳細設定", false, "接続タイムアウトや文字コードの指定はフェーズ 1 の後半で対応します。")
    ];
}
