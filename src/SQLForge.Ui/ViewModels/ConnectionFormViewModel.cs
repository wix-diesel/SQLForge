using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続ダイアログの入力欄。編集中の値をドラフトとして保持し、
/// エンティティへの変換は検証を通ったあと（ユースケース側）で行う。
///
/// 「一般」タブの欄はここが直に持ち、残り 3 枚のタブは
/// タブごとのビューモデル（<see cref="Ssh"/>・<see cref="Certificate"/>・<see cref="Advanced"/>）に
/// 預けてある。1 枚ぶんずつ読み書きできるので、ドラフトへの組み立てもここで束ねるだけで済む。
/// </summary>
public sealed partial class ConnectionFormViewModel(
    IPlatformProfile platform,
    IConnectionFilePrompt? files = null) : ObservableObject
{
    private static readonly string[] UrlAffectingProperties =
    [
        nameof(Driver), nameof(Host), nameof(Port), nameof(Database),
        nameof(User), nameof(Authentication), nameof(Tls)
    ];

    private readonly IPlatformProfile _platform = platform;
    private ConnectionProfileId _id = ConnectionProfileId.New();
    private bool _isLoading;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private EnvironmentChoiceViewModel _environment = EnvironmentChoiceViewModel.For(EnvironmentTag.Local);
    [ObservableProperty] private DatabaseDriver _driver = DatabaseDriver.SqlServer;
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private string _port = string.Empty;
    [ObservableProperty] private string _database = string.Empty;
    [ObservableProperty] private string _user = string.Empty;
    [ObservableProperty] private AuthenticationChoice _authentication = AuthenticationChoice.For(AuthenticationMethod.Password);
    [ObservableProperty] private TlsChoice _tls = TlsChoice.For(TlsMode.Prefer);
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isPasswordVisible;
    [ObservableProperty] private bool _storeInKeyring = true;
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private ConnectionValidationResult _validation = ConnectionValidationResult.Valid;

    /// <summary>「SSH トンネル」タブ。</summary>
    public SshTunnelFormViewModel Ssh { get; } = new(files);

    /// <summary>「TLS / SSL」タブ。</summary>
    public TlsCertificateFormViewModel Certificate { get; } = new(files);

    /// <summary>「詳細設定」タブ。</summary>
    public AdvancedConnectionFormViewModel Advanced { get; } = new();

    public IReadOnlyList<EnvironmentChoiceViewModel> EnvironmentChoices => EnvironmentChoiceViewModel.All;

    public IReadOnlyList<DatabaseDriver> DriverChoices => DatabaseDriver.All;

    public IReadOnlyList<AuthenticationChoice> AuthenticationChoices => AuthenticationChoice.All;

    public IReadOnlyList<TlsChoice> TlsChoices => TlsChoice.All;

    /// <summary>SQLite のようなファイル接続ではホスト・ポート・認証欄を伏せる。</summary>
    public bool SupportsNetworkAddress => !Driver.IsFileBased;

    public string HostLabel => Driver.IsFileBased ? "ファイル" : "ホスト";

    public bool RequiresPassword => SupportsNetworkAddress && Authentication.Method == AuthenticationMethod.Password;

    /// <summary>OS 統合認証を選んでいる状態。利用者名とパスワードは OS が受け持つ。</summary>
    public bool UsesIntegratedAuthentication =>
        SupportsNetworkAddress && Authentication.Method == AuthenticationMethod.Integrated;

    /// <summary>利用者名の欄を出すか。OS 統合認証では打っても使われないので伏せる。</summary>
    public bool RequiresUserName => SupportsNetworkAddress && !UsesIntegratedAuthentication;

    /// <summary>OS 統合認証で名乗るアカウント名。利用者名の欄の代わりに出す。</summary>
    public string IntegratedAccountName => _platform.IntegratedAccountName;

    /// <summary>Kerberos の用意が要る OS で、統合認証を選んだときだけ出す注意書き。</summary>
    public bool ShowsKerberosNotice =>
        UsesIntegratedAuthentication && _platform.IntegratedAuthenticationNeedsKerberos;

    /// <summary>本番接続を書き込み可で開こうとしている状態。トグルの下に警告を出す。</summary>
    public bool IsUnsafeWriteAccess => Environment.Tag.RequiresReadOnlyByDefault && !IsReadOnly;

    public string Url => ToDraft().ToUrl();

    public IReadOnlyList<ConnectionUrlPart> UrlParts => ConnectionUrlHighlighter.Split(Url);

    public string? NameError => Validation[ConnectionValidator.NameField];

    public string? HostError => Validation[ConnectionValidator.HostField];

    public string? PortError => Validation[ConnectionValidator.PortField];

    public string? DatabaseError => Validation[ConnectionValidator.DatabaseField];

    public string? UserError => Validation[ConnectionValidator.UserField];

    public bool HasNameError => NameError is not null;

    public bool HasHostError => HostError is not null;

    public bool HasPortError => PortError is not null;

    public bool HasDatabaseError => DatabaseError is not null;

    public bool HasUserError => UserError is not null;

    /// <summary>保存済み接続を選んだときに、その内容を入力欄へ写す。</summary>
    public void Load(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Load(ConnectionDraft.FromProfile(profile));
    }

    public void Load(ConnectionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        _isLoading = true;
        try
        {
            _id = draft.Id;
            Name = draft.Name;
            Environment = EnvironmentChoiceViewModel.For(draft.Environment);
            Driver = draft.Driver;
            Host = draft.Host;
            Port = draft.Port == 0 ? string.Empty : draft.Port.ToString();
            Database = draft.Database;
            User = draft.UserName;
            Authentication = AuthenticationChoice.For(draft.Authentication);
            Tls = TlsChoice.For(draft.Tls);
            StoreInKeyring = draft.StoreSecretInKeyring;
            IsReadOnly = draft.AccessMode == AccessMode.ReadOnly;
            Password = string.Empty;
            Validation = ConnectionValidationResult.Valid;

            Ssh.Load(draft.Tunnel);
            Certificate.Load(draft.Certificate);
            Advanced.Load(draft.Advanced);
        }
        finally
        {
            _isLoading = false;
        }

        RaiseDerivedChanged();
    }

    /// <summary>「新しい接続」。入力欄を初期値に戻す。</summary>
    public void LoadDraft() => Load(ConnectionDraft.FromProfile(ConnectionProfile.CreateDraft(DatabaseDriver.SqlServer)));

    public ConnectionDraft ToDraft() => new()
    {
        Id = _id,
        Name = Name.Trim(),
        Environment = Environment.Tag,
        Driver = Driver,
        Host = Host.Trim(),
        Port = ParsePort(),
        Database = Database.Trim(),
        UserName = User.Trim(),
        Authentication = Authentication.Method,
        StoreSecretInKeyring = StoreInKeyring,
        Tls = Tls.Mode,
        AccessMode = IsReadOnly ? AccessMode.ReadOnly : AccessMode.ReadWrite,
        Certificate = Certificate.ToSettings(),
        Tunnel = Ssh.ToSettings(),
        Advanced = Advanced.ToSettings()
    };

    private int ParsePort()
    {
        if (Driver.IsFileBased)
        {
            return 0;
        }

        return int.TryParse(Port.Trim(), out var value) ? value : 0;
    }

    partial void OnDriverChanged(DatabaseDriver value)
    {
        if (!_isLoading)
        {
            Port = value.IsFileBased ? string.Empty : value.DefaultPort.ToString();
            Database = value.DefaultDatabase;
        }

        RaiseDerivedChanged();
    }

    partial void OnEnvironmentChanged(EnvironmentChoiceViewModel value)
    {
        if (!_isLoading)
        {
            IsReadOnly = ConnectionProfile.DefaultAccessModeFor(value.Tag) == AccessMode.ReadOnly;
        }

        OnPropertyChanged(nameof(IsUnsafeWriteAccess));
    }

    partial void OnIsReadOnlyChanged(bool value) => OnPropertyChanged(nameof(IsUnsafeWriteAccess));

    // 認証方式を変えると、利用者名・パスワード・OS アカウントのどれを出すかが入れ替わる。
    partial void OnAuthenticationChanged(AuthenticationChoice value)
    {
        OnPropertyChanged(nameof(RequiresPassword));
        OnPropertyChanged(nameof(RequiresUserName));
        OnPropertyChanged(nameof(UsesIntegratedAuthentication));
        OnPropertyChanged(nameof(IntegratedAccountName));
        OnPropertyChanged(nameof(ShowsKerberosNotice));
    }

    // 「TLS / SSL」タブは要求レベルを変えられないが、今どの段なのかは出す。
    partial void OnTlsChanged(TlsChoice value) => Certificate.Tls = value.Mode;

    // 検証結果が変わったら、そこから導かれるエラー表示をまとめて更新する。
    // 別タブの欄のエラーも同じ検証結果から出すので、タブごとのビューモデルへも配る。
    partial void OnValidationChanged(ConnectionValidationResult value)
    {
        Ssh.Validation = value;
        Advanced.Validation = value;
        OnPropertyChanged(string.Empty);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is { } name && UrlAffectingProperties.Contains(name))
        {
            OnPropertyChanged(nameof(Url));
            OnPropertyChanged(nameof(UrlParts));
        }
    }

    private void RaiseDerivedChanged() => OnPropertyChanged(string.Empty);
}
