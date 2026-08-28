using System.Data.Common;
using Npgsql;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.PostgreSql;

/// <summary>
/// PostgreSQL 用の接続口。接続を開いた直後にサーバーの素性を読んでセッションに持たせる。
/// 受け持ちは SQL Server 側（<c>SqlServerConnector</c>）と同じで、訊き方だけが違う。
/// </summary>
public sealed class PostgreSqlConnector : IDatabaseConnector
{
    public DatabaseDriver Driver => DatabaseDriver.PostgreSql;

    public async Task<IDatabaseSession> ConnectAsync(
        ConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var connectionString = PostgreSqlConnectionStringFactory.Build(request);
        var connection = new NpgsqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var server = await ReadServerInfoAsync(connection, cancellationToken).ConfigureAwait(false);

            // 開いた時点で、SSH トンネルの後始末もこのセッションの持ち物になる
            // （開けなかったときに閉じるのは、トンネルを開いた呼び出し側の受け持ち）。
            // 接続文字列も渡す。データベースを移るときに開き直すのに要る。
            return new PostgreSqlSession(request.Profile, connection, server, connectionString, request.Tunnel);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<ServerInfo> ReadServerInfoAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlCatalogQueries.ServerInfo;

        var version = string.Empty;
        var banner = string.Empty;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                version = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                banner = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }

        // SQL Server の「エディション」に当たるものが無いので、そこは空のままにする。
        return new ServerInfo(
            PostgreSqlProductName.Describe(version, banner),
            version,
            string.Empty,
            await ReadEncryptionStateAsync(connection, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 経路が暗号化されたかをサーバーに訊く。pg_stat_ssl は PostgreSQL 9.5 以降にしか無く、
    /// 派生エンジンには持たないものもあるので、読めない接続では推測せずに「不明」(null) を返す。
    /// </summary>
    private static async Task<bool?> ReadEncryptionStateAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = PostgreSqlCatalogQueries.EncryptionState;

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return value is bool ssl ? ssl : null;
        }
        catch (DbException)
        {
            return null;
        }
    }
}
