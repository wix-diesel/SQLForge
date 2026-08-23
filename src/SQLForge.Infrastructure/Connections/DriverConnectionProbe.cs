using System.Data.Common;
using System.Diagnostics;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 実際に接続を開いてサーバーの素性を読み、すぐ閉じる接続テスト。
/// ドライバーの入っていない DBMS は、成功に見せかけず「未対応」として失敗を返す。
/// </summary>
public sealed class DriverConnectionProbe(IDatabaseConnectorRegistry registry) : IConnectionProbe
{
    private readonly IDatabaseConnectorRegistry _registry = registry;

    public async Task<ConnectionProbeResult> ProbeAsync(
        ConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var driver = request.Profile.Target.Driver;
        if (!_registry.TryResolve(driver, out var connector))
        {
            return ConnectionProbeResult.Failure(UnsupportedDriverMessage.For(driver, _registry.SupportedDrivers));
        }

        var watch = Stopwatch.StartNew();

        try
        {
            await using var session = await connector.ConnectAsync(request, cancellationToken).ConfigureAwait(false);
            watch.Stop();

            return ConnectionProbeResult.Success(
                session.Server.Description,
                DescribeTls(session.Server, request.Profile.Target.Tls),
                (int)watch.ElapsedMilliseconds);
        }
        catch (DbException exception)
        {
            return ConnectionProbeResult.Failure(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            // 接続文字列を組む段でドライバーが受け付けないと分かったもの（証明書認証など）。
            return ConnectionProbeResult.Failure(exception.Message);
        }
    }

    /// <summary>
    /// 実際に暗号化されたかをサーバーに訊けた場合はその事実を、
    /// 訊けなかった場合は「要求した設定」であることが分かる書き方をする。
    /// </summary>
    private static string DescribeTls(ServerInfo server, TlsMode requested) => server.IsEncrypted switch
    {
        true => "TLS 有効",
        false => "TLS なし",
        null => $"TLS 不明 (要求 {ToSslMode(requested)})"
    };

    private static string ToSslMode(TlsMode mode) => mode switch
    {
        TlsMode.Disabled => "disable",
        TlsMode.Prefer => "prefer",
        TlsMode.Require => "require",
        TlsMode.VerifyFull => "verify-full",
        _ => "prefer"
    };
}
