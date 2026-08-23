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
    public static ConnectionProbeResult Success(string serverVersion, string tlsSummary, int roundTripMs) =>
        new(true, "接続に成功", $"{serverVersion} · {tlsSummary} · 往復 {roundTripMs} ms");

    public static ConnectionProbeResult Failure(string reason) =>
        new(false, "接続に失敗", reason);
}
