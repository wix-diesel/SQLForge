using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// ユーザー マッピングの受け持ち。データベースの中のユーザーは名前ではなく SID でログインと
/// 結び付くので、まずログインの SID を取り、それを持つユーザーをデータベースごとに探す。
///
/// 1 度の照会でデータベースを横断することはできない（3 部名でも動的 SQL を組むしかない）ので、
/// アクセスできるデータベースを 1 つずつ当たる。読めないデータベースは黙って飛ばす。
/// </summary>
public sealed partial class SqlServerSession
{
    protected override async Task<IReadOnlyList<LoginUserMapping>> ReadLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken)
    {
        var sid = await ReadLoginSidAsync(connection, login, cancellationToken).ConfigureAwait(false);

        if (sid is null)
        {
            return [];
        }

        var mappings = new List<LoginUserMapping>();

        foreach (var database in await ReadAccessibleDatabasesAsync(connection, cancellationToken)
            .ConfigureAwait(false))
        {
            var mapping = await ReadMappingAsync(connection, database, sid, cancellationToken)
                .ConfigureAwait(false);

            if (mapping is not null)
            {
                mappings.Add(mapping);
            }
        }

        return mappings;
    }

    protected override async Task WriteLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired,
        CancellationToken cancellationToken)
    {
        foreach (var step in SqlServerMappingStatements.Plan(login, original, desired))
        {
            await ExecuteStatementsAsync(connection, step.Database, step.Statements, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<byte[]?> ReadLoginSidAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerMappingQueries.LoginSid;
        AddParameter(command, "@login", login.Value);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return value as byte[];
    }

    private static async Task<IReadOnlyList<DatabaseName>> ReadAccessibleDatabasesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerCatalogQueries.Databases;

        var databases = new List<DatabaseName>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // 開けないデータベース（オフライン・権限なし）は 3 部名でも読めない。
            if (reader.GetBoolean(2))
            {
                databases.Add(new DatabaseName(reader.GetString(0)));
            }
        }

        return databases;
    }

    /// <summary>
    /// 1 つのデータベースを当たる。ユーザーがいなければ null。
    /// 読めなかったデータベースも「いない」と同じ扱いにする（権限が無いだけで、
    /// ダイアログ全体を失敗させるほどのことではない）。
    /// </summary>
    private static async Task<LoginUserMapping?> ReadMappingAsync(
        DbConnection connection,
        DatabaseName database,
        byte[] sid,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Format(SqlServerMappingQueries.MappingFormat, database);
            AddParameter(command, "@sid", sid);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var user = new DatabaseUserName(reader.GetString(0));
            var schema = reader.IsDBNull(1) ? null : new SchemaName(reader.GetString(1));

            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

            var roles = new List<string>();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                roles.Add(reader.GetString(0));
            }

            return new LoginUserMapping(database, user, schema)
            {
                Roles = roles.Order(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }
        catch (DbException)
        {
            return null;
        }
    }
}
