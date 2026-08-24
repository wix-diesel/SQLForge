using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// セキュリティ（データベース ユーザー）の受け持ち。読み取りはカタログと同じ扱いだが、
/// 書き込みは複数の文に分かれる（CREATE USER と ALTER ROLE）ので、
/// まとめて 1 つのトランザクションで流すところまでをここで引き受ける。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task<IReadOnlyList<DatabaseUserDescriptor>> ListDatabaseUsersAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default) =>
        QueryAsync((connection, token) => ReadDatabaseUsersAsync(connection, database, token), cancellationToken);

    public Task<IReadOnlyList<string>> ListDatabaseRolesAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default) =>
        QueryAsync((connection, token) => ReadDatabaseRolesAsync(connection, database, token), cancellationToken);

    public Task CreateDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => CreateUserAsync(connection, database, definition, token),
            cancellationToken);
    }

    public Task AlterDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserDescriptor original,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => AlterUserAsync(connection, database, original, definition, token),
            cancellationToken);
    }

    public Task DropDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserName user,
        CancellationToken cancellationToken = default) =>
        WriteAsync((connection, token) => DropUserAsync(connection, database, user, token), cancellationToken);

    protected abstract Task<IReadOnlyList<DatabaseUserDescriptor>> ReadDatabaseUsersAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<string>> ReadDatabaseRolesAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken);

    protected abstract Task CreateUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task AlterUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserDescriptor original,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task DropUserAsync(
        DbConnection connection,
        DatabaseName database,
        DatabaseUserName user,
        CancellationToken cancellationToken);

    /// <summary>
    /// 実行先のデータベースへ切り替えてから、渡した文を順に流す。
    ///
    /// ユーザーの追加・編集は CREATE USER と ALTER ROLE のように複数の文に分かれるので、
    /// 途中で失敗したときに「ユーザーだけできてロールに入っていない」状態を残さないよう、
    /// ひとまとめのトランザクションにする。コミットへ辿り着かなければ破棄が巻き戻す。
    /// </summary>
    protected async Task ExecuteStatementsAsync(
        DbConnection connection,
        DatabaseName database,
        IReadOnlyList<string> statements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(statements);

        if (statements.Count == 0)
        {
            return;
        }

        // ユーザーの操作は 3 部名で書けないので、必ずそのデータベースの中で流す。
        await SwitchDatabaseAsync(connection, database, cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>戻り値を持たない操作。門の扱いは読み取りと同じにする。</summary>
    private Task WriteAsync(
        Func<DbConnection, CancellationToken, Task> write,
        CancellationToken cancellationToken) =>
        QueryAsync(
            async (connection, token) =>
            {
                await write(connection, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
}
