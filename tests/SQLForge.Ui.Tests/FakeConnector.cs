using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 実サーバーへ行かずに接続が開けたことにするドライバー。
/// どのパスワードで開こうとしたかを覚えておき、資格情報の受け渡しの確認に使う。
/// </summary>
public sealed class FakeConnector(DatabaseDriver? driver = null) : IDatabaseConnector
{
    public DatabaseDriver Driver { get; } = driver ?? DatabaseDriver.SqlServer;

    /// <summary>直近の接続要求。パスワードが何で渡ったかを見る。</summary>
    public ConnectionRequest? LastRequest { get; private set; }

    public int ConnectCount { get; private set; }

    /// <summary>接続の代わりに投げる例外。失敗したときの後始末を確かめるために差し込む。</summary>
    public Exception? Failure { get; set; }

    public Task<IDatabaseSession> ConnectAsync(ConnectionRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        ConnectCount++;

        if (Failure is { } failure)
        {
            throw failure;
        }

        // 本物のドライバーと同じく、開いた時点で経路の後始末もセッションの持ち物になる。
        return Task.FromResult<IDatabaseSession>(new FakeDatabaseSession(request.Profile) { Route = request.Tunnel });
    }
}
