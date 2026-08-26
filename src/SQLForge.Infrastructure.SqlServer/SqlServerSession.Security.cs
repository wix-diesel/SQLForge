using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// セキュリティ（データベース ユーザー）の受け持ち。読み取りは sys.database_principals から、
/// 追加・編集・削除は CREATE / ALTER / DROP USER で行う。
///
/// カタログの照会と違い、ユーザーの操作は 3 部名で書けない（CREATE USER は今いる
/// データベースにしか作れない）ため、実行の前にデータベースを切り替える。
/// </summary>
public sealed partial class SqlServerSession
{
    protected override async Task<IReadOnlyList<DatabaseUserDescriptor>> ReadDatabaseUsersAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerSecurityQueries.UsersFormat, database);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var users = new List<DatabaseUserDescriptor>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            users.Add(new DatabaseUserDescriptor(
                new DatabaseUserName(reader.GetString(0)),
                ToUserType(reader.GetString(1), reader.GetInt32(2)),
                LoginName: reader.IsDBNull(4) ? null : reader.GetString(4),
                DefaultSchema: reader.IsDBNull(3) ? null : new SchemaName(reader.GetString(3)),
                IsSystem: reader.GetBoolean(5)));
        }

        // 2 つめの結果セットがロールの所属。ユーザーごとにまとめ直して貼り付ける。
        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        var memberships = await ReadGroupedNamesAsync(reader, cancellationToken).ConfigureAwait(false);

        return users
            .Select(user => memberships.TryGetValue(user.Name.Value, out var roles) ? user with { Roles = roles } : user)
            .ToList();
    }

    protected override Task CreateUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(
            connection, database, SqlServerSecurityStatements.Create(definition), cancellationToken);

    protected override Task AlterUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDescriptor original,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(
            connection, database, SqlServerSecurityStatements.Alter(original, definition), cancellationToken);

    protected override Task DropUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserName user,
        CancellationToken cancellationToken) =>
        ExecuteStatementsAsync(
            connection, database, [SqlServerSecurityStatements.Drop(user)], cancellationToken);

    /// <summary>
    /// 「名前 → 名前の並び」の結果セットをまとめ直す。1 列目で束ね、2 列目を集める。
    /// ユーザーの所属ロール・ロールのメンバー・ロールの所有スキーマは、どれもこの形をしている。
    /// </summary>
    internal static async Task<Dictionary<string, IReadOnlyList<string>>> ReadGroupedNamesAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var memberships = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var member = reader.GetString(0);

            if (!memberships.TryGetValue(member, out var roles))
            {
                roles = [];
                memberships[member] = roles;
            }

            roles.Add(reader.GetString(1));
        }

        return memberships.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// sys.database_principals の type は SQL ユーザーのログインの有無を分けないので、
    /// authentication_type（0 = NONE = WITHOUT LOGIN）と併せて判じる。
    /// </summary>
    private static DatabaseUserType ToUserType(string typeCode, int authenticationType) => typeCode switch
    {
        "S" => authenticationType == 0 ? DatabaseUserType.SqlUserWithoutLogin : DatabaseUserType.SqlUserWithLogin,
        "U" => DatabaseUserType.WindowsUser,
        "G" => DatabaseUserType.WindowsGroup,
        "C" => DatabaseUserType.Certificate,
        "K" => DatabaseUserType.AsymmetricKey,
        "E" or "X" => DatabaseUserType.External,
        _ => DatabaseUserType.Unknown
    };
}
