using System.Diagnostics;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 疑似的な接続テスト。この版には DB ドライバーを入れていないので、
/// 実際には何も送らず、待ち時間と結果表示の見え方だけを再現する。
/// ドライバーを入れる際は、このクラスをドライバー別の実装に差し替える。
/// </summary>
public sealed class SimulatedConnectionProbe : IConnectionProbe
{
    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(420);

    public async Task<ConnectionProbeResult> ProbeAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var watch = Stopwatch.StartNew();
        await Task.Delay(SimulatedLatency, cancellationToken).ConfigureAwait(false);
        watch.Stop();

        return ConnectionProbeResult.Success(
            DescribeServer(profile.Target.Driver),
            DescribeTls(profile.Target.Tls),
            (int)watch.ElapsedMilliseconds);
    }

    private static string DescribeServer(DatabaseDriver driver) => driver.Id switch
    {
        "sqlserver" => "SQL Server 2022 (16.0)",
        "postgres" => "PostgreSQL 16.2",
        "mysql" => "MySQL 8.4",
        "clickhouse" => "ClickHouse 24.3",
        "sqlite" => "SQLite 3.45",
        _ => driver.DisplayName
    };

    private static string DescribeTls(TlsMode mode) => mode switch
    {
        TlsMode.Disabled => "TLS なし",
        TlsMode.Prefer => "TLS prefer",
        TlsMode.Require => "TLS require",
        TlsMode.VerifyFull => "TLS verify-full",
        _ => "TLS 不明"
    };
}
