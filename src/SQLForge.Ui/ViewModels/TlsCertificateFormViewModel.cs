using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続ダイアログ「TLS / SSL」タブの入力欄。
///
/// 暗号化を要求するかどうかは「一般」タブの TLS が決めるので、ここでは変えない
/// （同じことを 2 か所で決めると、どちらが効いているのか分からなくなる）。
/// このタブが持つのは、その要求を検証するときの材料 ―― SSMS の
/// 「Host Name in Certificate」と「サーバー証明書」に当たるものだけ。
/// </summary>
public sealed partial class TlsCertificateFormViewModel(IConnectionFilePrompt? files = null) : ObservableObject
{
    private readonly IConnectionFilePrompt? _files = files;

    [ObservableProperty] private string _hostNameInCertificate = string.Empty;
    [ObservableProperty] private string _serverCertificatePath = string.Empty;

    /// <summary>「一般」タブで選ばれている要求レベル。読むだけで、ここからは変えない。</summary>
    [ObservableProperty] private TlsMode _tls = TlsMode.Prefer;

    public bool CanBrowse => _files is not null;

    /// <summary>SSMS の [暗号化] に当たる言い方。「一般」タブの選択をそのまま言い換える。</summary>
    public string EncryptionName => Tls switch
    {
        TlsMode.Disabled or TlsMode.Prefer => "省略可能 (Optional)",
        TlsMode.Require or TlsMode.VerifyFull => "必須 (Mandatory)",
        TlsMode.Strict => "厳密 (Strict)",
        _ => "必須 (Mandatory)"
    };

    /// <summary>証明書を検証せずに信頼している状態（SSMS の Trust server certificate）。</summary>
    public bool TrustsServerCertificate => Tls is TlsMode.Disabled or TlsMode.Prefer or TlsMode.Require;

    public bool ValidatesCertificate => !TrustsServerCertificate;

    public string TrustSummary =>
        TrustsServerCertificate ? "サーバー証明書を信頼する（検証しない）" : "サーバー証明書を検証する";

    /// <summary>
    /// 検証しない要求レベルのときに出す断り書き。指定そのものは保存するが、
    /// 今の設定では使われないことを隠さない。
    /// </summary>
    public bool ShowsIgnoredNotice => TrustsServerCertificate && IsConfigured;

    public bool IsConfigured => ToSettings().IsConfigured;

    /// <summary>タブの見出しに出す印。</summary>
    public string Badge => IsConfigured ? "指定あり" : string.Empty;

    public void Load(TlsCertificateSettings certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        HostNameInCertificate = certificate.HostNameInCertificate;
        ServerCertificatePath = certificate.ServerCertificatePath;
    }

    public TlsCertificateSettings ToSettings() => new()
    {
        HostNameInCertificate = HostNameInCertificate,
        ServerCertificatePath = ServerCertificatePath
    };

    [RelayCommand]
    private async Task BrowseServerCertificateAsync()
    {
        if (_files is null)
        {
            return;
        }

        if (await _files.AskFileAsync("サーバー証明書を選ぶ").ConfigureAwait(true) is { } path)
        {
            ServerCertificatePath = path;
        }
    }

    partial void OnTlsChanged(TlsMode value) => OnPropertyChanged(string.Empty);

    partial void OnHostNameInCertificateChanged(string value) => RaiseConfiguredChanged();

    partial void OnServerCertificatePathChanged(string value) => RaiseConfiguredChanged();

    private void RaiseConfiguredChanged()
    {
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(ShowsIgnoredNotice));
        OnPropertyChanged(nameof(Badge));
    }
}
