using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続ダイアログ「一般」タブの入力欄。編集中の値をドラフトとして保持し、
/// エンティティへの変換は検証を通ったあと（ユースケース側）で行う。
/// </summary>
public sealed partial class ConnectionFormViewModel : ObservableObject
{
    private static readonly string[] UrlAffectingProperties =
    [
        nameof(Driver), nameof(Host), nameof(Port), nameof(Database),
        nameof(User), nameof(Authentication), nameof(Tls)
    ];

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

    public IReadOnlyList<EnvironmentChoiceViewModel> EnvironmentChoices => EnvironmentChoiceViewModel.All;

    public IReadOnlyList<DatabaseDriver> DriverChoices => DatabaseDriver.All;

    public IReadOnlyList<AuthenticationChoice> AuthenticationChoices => AuthenticationChoice.All;

    public IReadOnlyList<TlsChoice> TlsChoices => TlsChoice.All;

    /// <summary>SQLite のようなファイル接続ではホスト・ポート・認証欄を伏せる。</summary>
    public bool SupportsNetworkAddress => !Driver.IsFileBased;

    public string HostLabel => Driver.IsFileBased ? "ファイル" : "ホスト";

    public bool RequiresPassword => SupportsNetworkAddress && Authentication.Method == AuthenticationMethod.Password;

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
        AccessMode = IsReadOnly ? AccessMode.ReadOnly : AccessMode.ReadWrite
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

    partial void OnAuthenticationChanged(AuthenticationChoice value) => OnPropertyChanged(nameof(RequiresPassword));

    // 検証結果が変わったら、そこから導かれるエラー表示をまとめて更新する。
    partial void OnValidationChanged(ConnectionValidationResult value) => OnPropertyChanged(string.Empty);

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
