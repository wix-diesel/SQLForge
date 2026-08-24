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

    public Task<IDatabaseSession> ConnectAsync(ConnectionRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        ConnectCount++;

        return Task.FromResult<IDatabaseSession>(new FakeDatabaseSession(request.Profile));
    }
}
