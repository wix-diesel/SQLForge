using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// セキュリティ（サーバー ログイン）の受け持ち。読み取りは sys.server_principals と
/// sys.sql_logins から、追加・編集・削除は CREATE / ALTER / DROP LOGIN で行う。
///
/// どれもサーバー スコープなので、データベース ユーザーと違って USE で切り替える必要はない。
/// </summary>
public sealed partial class SqlServerSession
{
    protected override async Task<IReadOnlyList<ServerLoginDescriptor>> ReadServerLoginsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerLoginQueries.Logins;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var logins = new List<ServerLoginDescriptor>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var type = ToLoginType(reader.GetString(1));

            logins.Add(new ServerLoginDescriptor(
                new ServerLoginName(reader.GetString(0)),
                type,
                DefaultDatabase: reader.IsDBNull(2) ? null : new DatabaseName(reader.GetString(2)),
                IsDisabled: reader.GetBoolean(3),
                IsSystem: reader.GetBoolean(4))
            {
                PasswordPolicy = ReadPolicy(reader)
            });
        }

        // 2 つめの結果セットがロールの所属。ログインごとにまとめ直して貼り付ける。
        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

        var memberships = await ReadMembershipsAsync(reader, cancellationToken).ConfigureAwait(false);

        return logins
            .Select(login =>
                memberships.TryGetValue(login.Name.Value, out var roles) ? login with { Roles = roles } : login)
            .ToList();
    }

    protected override async Task<IReadOnlyList<string>> ReadServerRolesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlServerLoginQueries.Roles;

        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            roles.Add(reader.GetString(0));
        }

        return roles;
    }

    protected override Task CreateLoginAsync(
        DbConnection connection,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteServerStatementsAsync(connection, SqlServerLoginStatements.Create(definition), cancellationToken);

    protected override Task AlterLoginAsync(
        DbConnection connection,
        ServerLoginDescriptor original,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken) =>
        ExecuteServerStatementsAsync(
            connection, SqlServerLoginStatements.Alter(original, definition), cancellationToken);

    protected override Task DropLoginAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken) =>
        ExecuteServerStatementsAsync(connection, [SqlServerLoginStatements.Drop(login)], cancellationToken);

    /// <summary>
    /// パスワードの規則。SQL Server 認証以外のログインは sys.sql_logins に行が無いので、
    /// LEFT JOIN の結果は NULL になる。そのときは規則そのものを持たせない。
    /// </summary>
    private static ServerLoginPasswordPolicy? ReadPolicy(DbDataReader reader)
    {
        if (reader.IsDBNull(5) || reader.IsDBNull(6))
        {
            return null;
        }

        var enforcePolicy = reader.GetBoolean(5);

        // 規則を切ったログインでは期限だけが残っていることがある（CHECK_POLICY を落とした名残）。
        // 値オブジェクトはその組み合わせを許さないので、読むときに畳んでおく。
        return new ServerLoginPasswordPolicy(enforcePolicy, enforcePolicy && reader.GetBoolean(6));
    }

    /// <summary>
    /// sys.server_principals の type から種類を決める。データベース ユーザーと違い、
    /// SQL Server 認証のログインは type だけで見分けられる。
    /// </summary>
    private static ServerLoginType ToLoginType(string typeCode) => typeCode switch
    {
        "S" => ServerLoginType.SqlLogin,
        "U" => ServerLoginType.WindowsUser,
        "G" => ServerLoginType.WindowsGroup,
        "C" => ServerLoginType.Certificate,
        "K" => ServerLoginType.AsymmetricKey,
        "E" or "X" => ServerLoginType.External,
        _ => ServerLoginType.Unknown
    };
}
