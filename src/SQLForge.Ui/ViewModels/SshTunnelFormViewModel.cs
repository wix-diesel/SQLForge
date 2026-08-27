using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続ダイアログ「SSH トンネル」タブの入力欄。
/// SSMS にこのタブは無いので、踏み台の指定の並びは SQLForge のもの。
/// パスワードとパスフレーズの預け方だけ「一般」タブと同じにしてある。
/// </summary>
public sealed partial class SshTunnelFormViewModel(IConnectionFilePrompt? files = null) : ObservableObject
{
    private readonly IConnectionFilePrompt? _files = files;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private string _port = SshTunnelSettings.DefaultPort.ToString();
    [ObservableProperty] private string _user = string.Empty;

    [ObservableProperty]
    private SshAuthenticationChoice _authentication = SshAuthenticationChoice.For(SshAuthenticationMethod.Password);

    [ObservableProperty] private string _privateKeyPath = string.Empty;
    [ObservableProperty] private string _secret = string.Empty;
    [ObservableProperty] private bool _isSecretVisible;
    [ObservableProperty] private bool _storeInKeyring = true;
    [ObservableProperty] private string _localPort = string.Empty;
    [ObservableProperty] private ConnectionValidationResult _validation = ConnectionValidationResult.Valid;

    public IReadOnlyList<SshAuthenticationChoice> AuthenticationChoices => SshAuthenticationChoice.All;

    /// <summary>ファイル選択を出せる状態か。出せないところ（テストなど）では参照ボタンを伏せる。</summary>
    public bool CanBrowse => _files is not null;

    public bool UsesPassword => Authentication.Method == SshAuthenticationMethod.Password;

    /// <summary>パスワードとパスフレーズは同じ場所に預けるので、見出しだけを入れ替える。</summary>
    public string SecretLabel => UsesPassword ? "パスワード" : "パスフレーズ（鍵に掛かっていれば）";

    public bool RequiresPrivateKey => !UsesPassword;

    public string? HostError => Validation[ConnectionValidator.SshHostField];

    public string? PortError => Validation[ConnectionValidator.SshPortField];

    public string? UserError => Validation[ConnectionValidator.SshUserField];

    public string? PrivateKeyError => Validation[ConnectionValidator.SshKeyField];

    public string? LocalPortError => Validation[ConnectionValidator.SshLocalPortField];

    public bool HasHostError => HostError is not null;

    public bool HasPortError => PortError is not null;

    public bool HasUserError => UserError is not null;

    public bool HasPrivateKeyError => PrivateKeyError is not null;

    public bool HasLocalPortError => LocalPortError is not null;

    /// <summary>タブの見出しに出す印。使っている接続だと一目で分かるようにする。</summary>
    public string Badge => IsEnabled ? "使用中" : string.Empty;

    public void Load(SshTunnelSettings tunnel)
    {
        ArgumentNullException.ThrowIfNull(tunnel);

        IsEnabled = tunnel.IsEnabled;
        Host = tunnel.Host;
        Port = tunnel.Port.ToString();
        User = tunnel.UserName;
        Authentication = SshAuthenticationChoice.For(tunnel.Authentication);
        PrivateKeyPath = tunnel.PrivateKeyPath;
        LocalPort = tunnel.UsesAutomaticLocalPort ? string.Empty : tunnel.LocalPort.ToString();
        StoreInKeyring = tunnel.StoreSecretInKeyring;
        Secret = string.Empty;
        Validation = ConnectionValidationResult.Valid;
    }

    public SshTunnelSettings ToSettings() => new()
    {
        IsEnabled = IsEnabled,
        Host = Host,
        Port = ParseNumber(Port),
        UserName = User,
        Authentication = Authentication.Method,
        PrivateKeyPath = PrivateKeyPath,
        LocalPort = ParseLocalPort(LocalPort),
        StoreSecretInKeyring = StoreInKeyring
    };

    [RelayCommand]
    private async Task BrowsePrivateKeyAsync()
    {
        if (_files is null)
        {
            return;
        }

        if (await _files.AskFileAsync("秘密鍵を選ぶ").ConfigureAwait(true) is { } path)
        {
            PrivateKeyPath = path;
        }
    }

    /// <summary>読めない値は 0 にして検証で弾く（「打ちかけ」を勝手に既定値へ寄せない）。</summary>
    private static int ParseNumber(string text) => int.TryParse(text.Trim(), out var value) ? value : 0;

    /// <summary>空欄は「自動で選ぶ」。読めない値は -1 にして検証で弾く。</summary>
    private static int ParseLocalPort(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return 0;
        }

        return int.TryParse(trimmed, out var value) ? value : -1;
    }

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(Badge));

    partial void OnAuthenticationChanged(SshAuthenticationChoice value)
    {
        OnPropertyChanged(nameof(UsesPassword));
        OnPropertyChanged(nameof(RequiresPrivateKey));
        OnPropertyChanged(nameof(SecretLabel));
    }

    // 検証結果が変わったら、そこから導かれるエラー表示をまとめて更新する。
    partial void OnValidationChanged(ConnectionValidationResult value) => OnPropertyChanged(string.Empty);
}
