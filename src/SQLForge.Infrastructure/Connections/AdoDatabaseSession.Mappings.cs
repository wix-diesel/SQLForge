using System.Data.Common;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// ユーザー マッピングの受け持ち。ログイン 1 件を軸に、データベースを横断して
/// ユーザーを読んだり作ったりする。
///
/// 読み取りはデータベースごとの照会をひとつなぎにできるかがエンジン次第なので、
/// 書き込みと同じくドライバーへ任せる。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task<IReadOnlyList<LoginUserMapping>> ListLoginUserMappingsAsync(
        ServerLoginName login,
        CancellationToken cancellationToken = default) =>
        QueryAsync((connection, token) => ReadLoginUserMappingsAsync(connection, login, token), cancellationToken);

    public Task ApplyLoginUserMappingsAsync(
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(desired);

        return WriteAsync(
            (connection, token) => WriteLoginUserMappingsAsync(connection, login, original, desired, token),
            cancellationToken);
    }

    protected abstract Task<IReadOnlyList<LoginUserMapping>> ReadLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        CancellationToken cancellationToken);

    protected abstract Task WriteLoginUserMappingsAsync(
        DbConnection connection,
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired,
        CancellationToken cancellationToken);
}
