using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// セキュリティ保護可能なリソースと権限の受け持ち。
///
/// サーバー スコープの主体は sys.server_permissions を、データベース スコープの主体は
/// そのデータベースの sys.database_permissions を見る。書き込みも同じ分かれ方で、
/// データベース スコープのぶんは 3 部名で書けないので実行の前に切り替える。
/// </summary>
public sealed partial class SqlServerSession
{
    protected override Task<IReadOnlyList<PermissionEntry>> ReadPermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        CancellationToken cancellationToken) =>
        principal.IsServerScoped
            ? ReadServerPermissionsAsync(connection, principal, cancellationToken)
            : ReadDatabasePermissionsAsync(connection, principal, database, cancellationToken);

    protected override Task WritePermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired,
        CancellationToken cancellationToken)
    {
        var statements = SqlServerPermissionStatements.Changes(principal, original, desired);

        if (principal.IsServerScoped)
        {
            return ExecuteServerStatementsAsync(connection, statements, cancellationToken);
        }

        // データベース スコープの主体には必ず居場所がある（ユースケースが先に見ている）。
        var target = database ?? throw new ArgumentNullException(
            nameof(database),
            "データベース スコープの主体には、権限を書くデータベースが要ります。");

        return ExecuteStatementsAsync(connection, target, statements, cancellationToken);
    }

    protected override async Task<IReadOnlyList<SecurableReference>> ReadSecurablesAsync(
        DbConnection connection,
        SecurableKind kind,
        DatabaseName? database,
        CancellationToken cancellationToken)
    {
        // データベース スコープの候補は、どのデータベースを見るのかが決まっていないと読めない。
        if (kind is not (SecurableKind.Server or SecurableKind.Login) && database is null)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = SecurableQuery(kind, database);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var securables = new List<SecurableReference>();
        var qualified = kind.IsSchemaQualified();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            securables.Add(qualified
                ? new SecurableReference(kind, reader.GetString(1), reader.GetString(0))
                : new SecurableReference(kind, reader.GetString(0)));
        }

        return securables;
    }

    private static string SecurableQuery(SecurableKind kind, DatabaseName? database) => kind switch
    {
        SecurableKind.Server => SqlServerPermissionQueries.ServerName,
        SecurableKind.Login => SqlServerPermissionQueries.Logins,
        SecurableKind.Database => SqlServerPermissionQueries.Databases,
        SecurableKind.Schema => Format(SqlServerPermissionQueries.SchemasFormat, database!.Value),
        SecurableKind.Table => Format(SqlServerPermissionQueries.TablesFormat, database!.Value),
        SecurableKind.StoredProcedure =>
            Format(SqlServerPermissionQueries.StoredProceduresFormat, database!.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "知らない種類です。")
    };

    private async Task<IReadOnlyList<PermissionEntry>> ReadServerPermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerPermissionQueries.ServerPermissions;
        AddParameter(command, "@principal", principal.Name);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var entries = new List<PermissionEntry>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // class 100 はサーバーそのもの、101 はログイン。相手の名前が読めない行は出さない。
            var securable = reader.GetInt32(0) == 100
                ? SecurableReference.Server(Profile.Name)
                : reader.IsDBNull(3) ? null : new SecurableReference(SecurableKind.Login, reader.GetString(3));

            if (securable is not null)
            {
                entries.Add(new PermissionEntry(securable, reader.GetString(1), ToState(reader.GetString(2))));
            }
        }

        return entries;
    }

    private static async Task<IReadOnlyList<PermissionEntry>> ReadDatabasePermissionsAsync(
        DbConnection connection,
        SecurityPrincipal principal,
        DatabaseName? database,
        CancellationToken cancellationToken)
    {
        if (database is not { } target)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerPermissionQueries.DatabasePermissionsFormat, target);
        AddParameter(command, "@principal", principal.Name);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var entries = new List<PermissionEntry>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (ToDatabaseSecurable(reader, target) is { } securable)
            {
                entries.Add(new PermissionEntry(securable, reader.GetString(1), ToState(reader.GetString(2))));
            }
        }

        return entries;
    }

    /// <summary>
    /// class 0 はデータベースそのもの、3 はスキーマ、1 はオブジェクト。
    /// オブジェクトのうち、この版が扱うのはテーブルとストアド プロシージャだけで、
    /// それ以外（ビュー・関数など）に付いた権限は一覧に出さない。
    /// </summary>
    private static SecurableReference? ToDatabaseSecurable(DbDataReader reader, DatabaseName database) =>
        reader.GetInt32(0) switch
        {
            0 => new SecurableReference(SecurableKind.Database, database.Value),
            3 => reader.IsDBNull(3) ? null : new SecurableReference(SecurableKind.Schema, reader.GetString(3)),
            1 => ToObjectSecurable(reader),
            _ => null
        };

    private static SecurableReference? ToObjectSecurable(DbDataReader reader)
    {
        if (reader.IsDBNull(4) || reader.IsDBNull(5) || reader.IsDBNull(6))
        {
            return null;
        }

        // sys.objects の type は char(2) で右詰めの空白が付く（'U ' や 'P '）。
        var kind = reader.GetString(6).Trim() switch
        {
            "U" => SecurableKind.Table,
            "P" or "PC" => SecurableKind.StoredProcedure,
            _ => (SecurableKind?)null
        };

        return kind is { } value
            ? new SecurableReference(value, reader.GetString(4), reader.GetString(5))
            : null;
    }

    /// <summary>
    /// sys.*_permissions の state_desc を写す。REVOKE の行はそもそも「明示的な指定なし」なので、
    /// 一覧に出ても状態としては何も付いていないのと同じになる。
    /// </summary>
    private static PermissionState ToState(string state) => state switch
    {
        "GRANT" => PermissionState.Granted,
        "GRANT_WITH_GRANT_OPTION" => PermissionState.GrantedWithGrantOption,
        "DENY" => PermissionState.Denied,
        _ => PermissionState.Revoked
    };
}
