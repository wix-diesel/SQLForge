using System.Data.Common;
using Microsoft.Data.SqlClient;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// SQL Server 用の接続口。接続を開いた直後にサーバーの素性を読んでセッションに持たせる。
/// </summary>
public sealed class SqlServerConnector : IDatabaseConnector
{
    public DatabaseDriver Driver => DatabaseDriver.SqlServer;

    public async Task<IDatabaseSession> ConnectAsync(
        ConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var connection = new SqlConnection(SqlServerConnectionStringFactory.Build(request));

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var server = await ReadServerInfoAsync(connection, cancellationToken).ConfigureAwait(false);

            return new SqlServerSession(request.Profile, connection, server);
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
        command.CommandText = SqlServerCatalogQueries.ServerInfo;

        var version = string.Empty;
        var edition = string.Empty;
        var engineEdition = 0;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                version = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                edition = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                engineEdition = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            }
        }

        return new ServerInfo(
            SqlServerProductName.Describe(version, engineEdition),
            version,
            edition,
            await ReadEncryptionStateAsync(connection, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 経路が暗号化されたかをサーバーに訊く。この DMV は VIEW SERVER STATE 権限が要るので、
    /// 読めない接続では推測せずに「不明」(null) を返す。
    /// </summary>
    private static async Task<bool?> ReadEncryptionStateAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = SqlServerCatalogQueries.EncryptionState;

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return value is string option
                ? string.Equals(option, "TRUE", StringComparison.OrdinalIgnoreCase)
                : null;
        }
        catch (DbException)
        {
            return null;
        }
    }
}
