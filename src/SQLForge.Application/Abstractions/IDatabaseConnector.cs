using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Abstractions;

/// <summary>
/// ドライバー 1 種類ぶんの接続口。DBMS を増やすときは、この実装と
/// <see cref="IDatabaseSession"/> の実装を Infrastructure に足して合成ルートへ登録するだけでよい。
/// </summary>
public interface IDatabaseConnector
{
    /// <summary>この実装が受け持つドライバー。</summary>
    DatabaseDriver Driver { get; }

    /// <summary>接続を開く。失敗は <see cref="System.Data.Common.DbException"/> などで投げる。</summary>
    Task<IDatabaseSession> ConnectAsync(ConnectionRequest request, CancellationToken cancellationToken = default);
}
