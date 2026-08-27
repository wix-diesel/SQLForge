using SQLForge.Application.Connections;

namespace SQLForge.Application.Abstractions;

/// <summary>
/// 接続の到達確認ポート（ダイアログの「接続をテスト」）。
/// 実装は実際に接続を開いて素性を読み、すぐ閉じる。
/// </summary>
public interface IConnectionProbe
{
    Task<ConnectionProbeResult> ProbeAsync(ConnectionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>接続テストの結果。ダイアログのフッターにそのまま出す。</summary>
public sealed record ConnectionProbeResult(
    bool Succeeded,
    string Headline,
    string Detail)
{
    /// <summary>
    /// 到達できたときの結果。<paramref name="tunnel"/> は経由した踏み台の説明で、
    /// SSH トンネルを通らなかった接続では空。
    /// </summary>
    public static ConnectionProbeResult Success(
        string serverVersion,
        string tlsSummary,
        int roundTripMs,
        string tunnel = "")
    {
        var detail = $"{serverVersion} · {tlsSummary} · 往復 {roundTripMs} ms";

        return new(true, "接続に成功", tunnel.Length > 0 ? $"{detail} · {tunnel}" : detail);
    }

    public static ConnectionProbeResult Failure(string reason) =>
        new(false, "接続に失敗", reason);
}
