namespace SQLForge.Domain.Connections;

/// <summary>接続に使うネットワーク プロトコル。SSMS の「ネットワーク プロトコル」に当たる。</summary>
public enum NetworkProtocol
{
    /// <summary>クライアントの設定に任せる（SSMS の &lt;既定値&gt;）。</summary>
    Default,

    /// <summary>TCP/IP。</summary>
    TcpIp,

    /// <summary>名前付きパイプ。</summary>
    NamedPipes,

    /// <summary>共有メモリ。同じ機械の中でだけ通る。</summary>
    SharedMemory
}

/// <summary>
/// 接続ダイアログ「詳細設定」タブの内容。SSMS の [接続プロパティ] と
/// [追加の接続パラメーター] に当たる。既定値も SSMS に合わせてある。
/// </summary>
public sealed record AdvancedConnectionSettings
{
    /// <summary>ネットワーク パケット サイズの既定（バイト）。</summary>
    public const int DefaultPacketSize = 4096;

    public const int MinPacketSize = 512;
    public const int MaxPacketSize = 32768;

    /// <summary>接続確立の待ち時間の既定（秒）。</summary>
    public const int DefaultConnectTimeoutSeconds = 15;

    /// <summary>文面の実行の待ち時間の既定（秒）。0 は待ち続ける。</summary>
    public const int DefaultExecutionTimeoutSeconds = 0;

    public const int MaxTimeoutSeconds = 65535;

    private readonly string _additionalParameters = string.Empty;

    /// <summary>何も変えていない状態。</summary>
    public static AdvancedConnectionSettings Default { get; } = new();

    public NetworkProtocol Protocol { get; init; }

    /// <summary>ネットワーク パケット サイズ（バイト）。</summary>
    public int PacketSize { get; init; } = DefaultPacketSize;

    /// <summary>接続確立の待ち時間（秒）。</summary>
    public int ConnectTimeoutSeconds { get; init; } = DefaultConnectTimeoutSeconds;

    /// <summary>文面 1 つの実行の待ち時間（秒）。0 は待ち続ける（SSMS と同じ）。</summary>
    public int ExecutionTimeoutSeconds { get; init; } = DefaultExecutionTimeoutSeconds;

    /// <summary>
    /// 接続文字列へそのまま足す指定（<c>キー=値;</c> の並び）。
    /// SSMS の [追加の接続パラメーター] と同じで、ここで書いたものが他の欄より優先される。
    /// </summary>
    public string AdditionalParameters
    {
        get => _additionalParameters;
        init => _additionalParameters = value?.Trim() ?? string.Empty;
    }

    public bool HasAdditionalParameters => AdditionalParameters.Length > 0;

    /// <summary>実行の待ち時間を設けない状態。</summary>
    public bool WaitsForExecutionForever => ExecutionTimeoutSeconds == DefaultExecutionTimeoutSeconds;

    /// <summary>既定のままか。タブに印を出すのに使う。</summary>
    public bool IsDefault => Equals(Default);

    public static bool IsValidPacketSize(int value) => value is >= MinPacketSize and <= MaxPacketSize;

    public static bool IsValidTimeout(int seconds) => seconds is >= 0 and <= MaxTimeoutSeconds;
}
