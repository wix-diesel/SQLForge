using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// セキュリティ（ロール）の受け持ち。データベース ロールは sys.database_principals から、
/// サーバー ロールは sys.server_principals から読む。
///
/// データベース ロールの操作は 3 部名で書けない（CREATE ROLE は今いるデータベースに作る）ので
/// 実行の前にデータベースを切り替えるが、サーバー ロールはその要りがない。
/// </summary>
public sealed partial class SqlServerSession
{
    protected override async Task<IReadOnlyList<DatabaseRoleDescriptor>> ReadDatabaseRolesAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerRoleQueries.RolesFormat, database);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var roles = new List<DatabaseRoleDescriptor>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            roles.Add(new DatabaseRoleDescriptor(
                new RoleName(reader.GetString(0)),
                Owner: reader.IsDBNull(1) ? null : reader.GetString(1),
                IsFixedRole: reader.GetBoolean(2)));
        }

        // 2 つめがメンバー、3 つめが所有スキーマ。どちらもロール名で束ねて貼り付ける。
        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        var members = await ReadGroupedNamesAsync(reader, cancellationToken).ConfigureAwait(false);

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        var schemas = await ReadGroupedNamesAsync(reader, cancellationToken).ConfigureAwait(false);

        return roles
            .Select(role =>
            {
                members.TryGetValue(role.Name.Value, out var inside);
                schemas.TryGetValue(role.Name.Value, out var owned);

                return role with { Members = inside ?? [], OwnedSchemas = owned ?? [] };
            })
            .ToList();
    }

    protected override async Task<IReadOnlyList<ServerRoleDescriptor>> ReadServerRolesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerRoleQueries.ServerRoles;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var roles = new List<ServerRoleDescriptor>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            roles.Add(new ServerRoleDescriptor(
                new RoleName(reader.GetString(0)),
                Owner: reader.IsDBNull(1) ? null : reader.GetString(1),
                IsFixedRole: reader.GetBoolean(2)));
        }

        // 2 つめが所属。ロールから見ればメンバー、メンバーの側がロールならメンバーシップになる。
        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        var (members, memberships) = await ReadServerRoleMembersAsync(reader, cancellationToken)
            .ConfigureAwait(false);

        return roles
            .Select(role =>
            {
                members.TryGetValue(role.Name.Value, out var inside);
                memberships.TryGetValue(role.Name.Value, out var outside);

                return role with { Members = inside ?? [], Memberships = outside ?? [] };
            })
            .ToList();
    }

    protected override Task CreateDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(
            connection, database, SqlServerDatabaseRoleStatements.Create(definition), cancellationToken);

    protected override Task AlterDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseRoleDescriptor original,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(
            connection, database, SqlServerDatabaseRoleStatements.Alter(original, definition), cancellationToken);

    protected override Task DropDatabaseRoleAsync(
        DbConnection connection,
        DatabaseName database,
        RoleName role,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(
            connection, database, [SqlServerDatabaseRoleStatements.Drop(role)], cancellationToken);

    protected override Task CreateServerRoleAsync(
        DbConnection connection,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteServerStatementsAsync(connection, SqlServerServerRoleStatements.Create(definition), cancellationToken);

    protected override Task AlterServerRoleAsync(
        DbConnection connection,
        ServerRoleDescriptor original,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteServerStatementsAsync(
            connection, SqlServerServerRoleStatements.Alter(original, definition), cancellationToken);

    protected override Task DropServerRoleAsync(
        DbConnection connection,
        RoleName role,
        CancellationToken cancellationToken) =>
        ExecuteServerStatementsAsync(connection, [SqlServerServerRoleStatements.Drop(role)], cancellationToken);

    /// <summary>
    /// サーバー ロールの所属を 1 度読み、2 通りにまとめ直す。
    /// ロール名で束ねればメンバー、メンバーがロールのときにその名前で束ねればメンバーシップになる。
    /// </summary>
    private static async Task<(Dictionary<string, IReadOnlyList<string>> Members,
        Dictionary<string, IReadOnlyList<string>> Memberships)> ReadServerRoleMembersAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var members = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var memberships = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var role = reader.GetString(0);
            var member = reader.GetString(1);

            Add(members, role, member);

            if (string.Equals(reader.GetString(2), "R", StringComparison.Ordinal))
            {
                Add(memberships, member, role);
            }
        }

        return (Freeze(members), Freeze(memberships));
    }

    private static void Add(Dictionary<string, List<string>> groups, string key, string value)
    {
        if (!groups.TryGetValue(key, out var names))
        {
            names = [];
            groups[key] = names;
        }

        names.Add(value);
    }

    private static Dictionary<string, IReadOnlyList<string>> Freeze(Dictionary<string, List<string>> groups) =>
        groups.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.Ordinal);
}
