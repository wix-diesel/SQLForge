using System.Data.Common;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// セキュリティ（サーバー ログイン）の受け持ち。データベース ユーザーと違って
/// スコープがサーバーなので、実行の前にデータベースを切り替える必要はない。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task<IReadOnlyList<ServerLoginDescriptor>> ListServerLoginsAsync(
        CancellationToken cancellationToken = default) =>
        QueryAsync(ReadServerLoginsAsync, cancellationToken);

    public Task<IReadOnlyList<string>> ListServerRolesAsync(CancellationToken cancellationToken = default) =>
        QueryAsync(ReadServerRolesAsync, cancellationToken);

    public Task CreateServerLoginAsync(
        ServerLoginDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync((connection, token) => CreateLoginAsync(connection, definition, token), cancellationToken);
    }

    public Task AlterServerLoginAsync(
        ServerLoginDescriptor original,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        return WriteAsync(
            (connection, token) => AlterLoginAsync(connection, original, definition, token),
            cancellationToken);
    }

    public Task DropServerLoginAsync(ServerLoginName login, CancellationToken cancellationToken = default) =>
        WriteAsync((connection, token) => DropLoginAsync(connection, login, token), cancellationToken);

    protected abstract Task<IReadOnlyList<ServerLoginDescriptor>> ReadServerLoginsAsync(
        DbConnection connection,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<string>> ReadServerRolesAsync(
        DbConnection connection,
        CancellationToken cancellationToken);

    protected abstract Task CreateLoginAsync(
        DbConnection connection,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task AlterLoginAsync(
        DbConnection connection,
        ServerLoginDescriptor original,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken);

    protected abstract Task DropLoginAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken);

    /// <summary>
    /// サーバー スコープの文を順に流す。データベース ユーザーの側と違って、
    /// トランザクションでは包まない。
    ///
    /// ログインの操作はサーバー スコープの DDL で、エンジンやパスワード ポリシーの都合により
    /// 巻き戻せる保証が無い。ひとまとめに包むと「戻せたつもり」になるだけなので、
    /// 1 文ずつ流して失敗したらそこで止め、理由をそのまま呼び出し側へ返す。
    /// 途中まで通ったぶんは残るが、一覧を読み直せば実際の姿がそのまま出る。
    /// </summary>
    protected static async Task ExecuteServerStatementsAsync(
        DbConnection connection,
        IReadOnlyList<string> statements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(statements);

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
