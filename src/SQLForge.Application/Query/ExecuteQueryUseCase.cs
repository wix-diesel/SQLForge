using SQLForge.Application.Abstractions;
using SQLForge.Domain.Query;

namespace SQLForge.Application.Query;

/// <summary>
/// エディタの文面を 1 回実行する。
///
/// 読み取り専用で開いた接続では、書き込みの文面をサーバーへ送る前に弾く。
/// 送ってからエラーで返ってくるのに任せると、弾けるのは権限のない接続だけになり、
/// 「読み取り専用で開いた」という利用者の意思が効かなくなるため。
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

        if (session.Profile.IsReadOnly && ReadOnlyQueryGuard.FindWriteKeyword(request.Sql) is { } keyword)
        {
            throw new QueryRejectedException(
                $"読み取り専用で開いた接続では {keyword} を実行できません。" +
                "書き込むには、接続し直してアクセス種別を「読み書き」にしてください。");
        }

        return await session
            .ExecuteQueryAsync(request.Database, request.Sql, request.MaxRows, cancellationToken)
            .ConfigureAwait(false);
    }
}
