using SQLForge.Application.Abstractions;
using SQLForge.Domain.Query;

namespace SQLForge.Application.Query;

/// <summary>
/// エディタの文面を 1 回実行する。
///
/// 文面は書き換えずにそのまま送る。読み書きの別はサーバー側の権限が決めることにして、
/// クライアントでは止めない（文面から書き込みかどうかを見分ける仕掛けは、
/// 動的 SQL のような形をどうせ通してしまうので、あると誤解を招く）。
/// </summary>
public sealed class ExecuteQueryUseCase
{
    /// <summary>結果セットごとに読む行数の既定の上限（モックアップの「取得上限 1,000 行」）。</summary>
    public const int DefaultMaxRows = 1_000;

    public async Task<QueryResult> ExecuteAsync(
        IDatabaseSession session,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Sql.Length == 0)
        {
            throw new QueryRejectedException("実行する文がありません。");
        }

        return await session
            .ExecuteQueryAsync(request.Database, request.Sql, request.MaxRows, cancellationToken)
            .ConfigureAwait(false);
    }
}
