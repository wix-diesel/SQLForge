using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続ダイアログ「詳細設定」タブの入力欄。項目も既定値も SSMS の
/// [接続プロパティ] と [追加の接続パラメーター] に合わせてある。
/// </summary>
public sealed partial class AdvancedConnectionFormViewModel : ObservableObject
{
    [ObservableProperty]
    private NetworkProtocolChoice _protocol = NetworkProtocolChoice.For(NetworkProtocol.Default);

    [ObservableProperty] private string _packetSize = AdvancedConnectionSettings.DefaultPacketSize.ToString();

    [ObservableProperty]
    private string _connectTimeout = AdvancedConnectionSettings.DefaultConnectTimeoutSeconds.ToString();

    [ObservableProperty]
    private string _executionTimeout = AdvancedConnectionSettings.DefaultExecutionTimeoutSeconds.ToString();

    [ObservableProperty] private string _additionalParameters = string.Empty;
    [ObservableProperty] private ConnectionValidationResult _validation = ConnectionValidationResult.Valid;

    public IReadOnlyList<NetworkProtocolChoice> ProtocolChoices => NetworkProtocolChoice.All;

    public string? PacketSizeError => Validation[ConnectionValidator.PacketSizeField];

    public string? ConnectTimeoutError => Validation[ConnectionValidator.ConnectTimeoutField];

    public string? ExecutionTimeoutError => Validation[ConnectionValidator.ExecutionTimeoutField];

    public bool HasPacketSizeError => PacketSizeError is not null;

    public bool HasConnectTimeoutError => ConnectTimeoutError is not null;

    public bool HasExecutionTimeoutError => ExecutionTimeoutError is not null;

    /// <summary>実行タイムアウトに 0 を入れてある状態。「待ち続ける」ことを添えて出す。</summary>
    public bool WaitsForExecutionForever => ToSettings().WaitsForExecutionForever;

    public bool IsDefault => ToSettings().IsDefault;

    /// <summary>タブの見出しに出す印。</summary>
    public string Badge => IsDefault ? string.Empty : "変更あり";

    public void Load(AdvancedConnectionSettings advanced)
    {
        ArgumentNullException.ThrowIfNull(advanced);

        Protocol = NetworkProtocolChoice.For(advanced.Protocol);
        PacketSize = advanced.PacketSize.ToString();
        ConnectTimeout = advanced.ConnectTimeoutSeconds.ToString();
        ExecutionTimeout = advanced.ExecutionTimeoutSeconds.ToString();
        AdditionalParameters = advanced.AdditionalParameters;
        Validation = ConnectionValidationResult.Valid;
    }

    public AdvancedConnectionSettings ToSettings() => new()
    {
        Protocol = Protocol.Protocol,
        PacketSize = ParseNumber(PacketSize, AdvancedConnectionSettings.DefaultPacketSize),
        ConnectTimeoutSeconds = ParseNumber(ConnectTimeout, AdvancedConnectionSettings.DefaultConnectTimeoutSeconds),
        ExecutionTimeoutSeconds =
            ParseNumber(ExecutionTimeout, AdvancedConnectionSettings.DefaultExecutionTimeoutSeconds),
        AdditionalParameters = AdditionalParameters
    };

    /// <summary>SSMS の [すべてリセット] と同じ。この 1 枚だけを既定値へ戻す。</summary>
    [RelayCommand]
    private void Reset() => Load(AdvancedConnectionSettings.Default);

    /// <summary>
    /// 空欄は既定値として扱い（SSMS も欄を空にすると既定に戻る）、
    /// 数字として読めない値は -1 にして検証で弾く。
    /// </summary>
    private static int ParseNumber(string text, int fallback)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return fallback;
        }

        return int.TryParse(trimmed, out var value) ? value : -1;
    }

    partial void OnValidationChanged(ConnectionValidationResult value) => OnPropertyChanged(string.Empty);

    partial void OnProtocolChanged(NetworkProtocolChoice value) => RaiseDerivedChanged();

    partial void OnPacketSizeChanged(string value) => RaiseDerivedChanged();

    partial void OnConnectTimeoutChanged(string value) => RaiseDerivedChanged();

    partial void OnExecutionTimeoutChanged(string value) => RaiseDerivedChanged();

    partial void OnAdditionalParametersChanged(string value) => RaiseDerivedChanged();

    private void RaiseDerivedChanged()
    {
        OnPropertyChanged(nameof(WaitsForExecutionForever));
        OnPropertyChanged(nameof(IsDefault));
        OnPropertyChanged(nameof(Badge));
    }
}
