using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// SSH トンネルを通す接続の筋書き。踏み台は立てず、開いた・閉じたの数だけを見る。
///
/// ここで押さえたいのは 2 つ。繋ぎ先が手元の待ち受け口へ差し替わること、
/// そして開いたトンネルが必ず閉じること（開きっぱなしにすると、
/// 踏み台への接続がアプリの終了まで残る）。
/// </summary>
public class SshTunnelUseCaseTests
{
    [Fact]
    public async Task トンネルを使う接続では手元の待ち受け口へ繋ぐ()
    {
        var (useCase, connector, broker) = Setup();

        var result = await useCase.ExecuteAsync(Draft(Tunnel()), new ConnectionSecrets(null, "hunter2"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, broker.OpenCount);
        Assert.Equal("127.0.0.1", connector.LastRequest!.Endpoint.Host);
        Assert.Equal(43317, connector.LastRequest!.Endpoint.Port);

        // 差し替わるのは繋ぎに行く先だけで、保存内容の繋ぎ先は本来のホストのまま。
        Assert.Equal("db.internal", connector.LastRequest!.Profile.Target.Address.Host);

        await result.Session!.DisposeAsync();
    }

    [Fact]
    public async Task 踏み台のパスワードは接続要求に添えて渡る()
    {
        var (useCase, _, broker) = Setup();

        var result = await useCase.ExecuteAsync(Draft(Tunnel()), new ConnectionSecrets("db", "hunter2"));

        Assert.Equal("hunter2", broker.LastRequest?.Secret);
        Assert.Equal("db.internal", broker.LastRequest?.Destination.Host);

        await result.Session!.DisposeAsync();
    }

    [Fact]
    public async Task セッションを閉じるとトンネルも閉じる()
    {
        var (useCase, _, broker) = Setup();

        var result = await useCase.ExecuteAsync(Draft(Tunnel()), new ConnectionSecrets(null, "hunter2"));
        Assert.Equal(0, broker.ClosedCount);

        await result.Session!.DisposeAsync();

        Assert.Equal(1, broker.ClosedCount);
    }

    [Fact]
    public async Task 接続に失敗したらトンネルを閉じる()
    {
        var (useCase, connector, broker) = Setup();
        connector.Failure = new NotSupportedException("繋げません。");

        var result = await useCase.ExecuteAsync(Draft(Tunnel()), new ConnectionSecrets(null, "hunter2"));

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        Assert.Equal(1, broker.OpenCount);
        Assert.Equal(1, broker.ClosedCount);
    }

    [Fact]
    public async Task トンネルを開けなければ理由をそのまま出す()
    {
        var (useCase, connector, broker) = Setup();
        broker.FailWith = "踏み台に名乗れませんでした。";

        var result = await useCase.ExecuteAsync(Draft(Tunnel()), new ConnectionSecrets(null, "wrong"));

        Assert.False(result.Succeeded);
        Assert.Equal("踏み台に名乗れませんでした。", result.Detail);

        // 経路が用意できていない以上、DB へは行かない（失敗の理由を取り違えないため）。
        Assert.Equal(0, connector.ConnectCount);
    }

    [Fact]
    public async Task トンネルを使わない接続では踏み台を呼ばない()
    {
        var (useCase, connector, broker) = Setup();

        var result = await useCase.ExecuteAsync(Draft(SshTunnelSettings.Disabled), new ConnectionSecrets("db"));

        Assert.Equal(0, broker.OpenCount);
        Assert.Equal("db.internal", connector.LastRequest!.Endpoint.Host);

        await result.Session!.DisposeAsync();
    }

    [Fact]
    public async Task 接続テストは開いたトンネルをその場で閉じる()
    {
        var broker = new FakeSshTunnelBroker();
        var connector = new FakeConnector();
        var useCase = new TestConnectionUseCase(
            new DriverConnectionProbe(new DatabaseConnectorRegistry([connector])),
            new ConnectionSecretResolver(new InMemorySecretStore()),
            new ConnectionTunnelOpener(broker));

        var result = await useCase.ExecuteAsync(Draft(Tunnel()), new ConnectionSecrets(null, "hunter2"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, broker.OpenCount);
        Assert.Equal(1, broker.ClosedCount);

        // 経由した踏み台は結果に出す（どの経路で届いたのかを隠さない）。
        Assert.Contains("alice@bastion.internal:22", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 踏み台のパスワードもキーリングから読む()
    {
        var store = new InMemorySecretStore();
        var broker = new FakeSshTunnelBroker();
        var connector = new FakeConnector();
        var draft = Draft(Tunnel());
        var profile = draft.ToProfile();

        await store.SaveAsync(SaveConnectionUseCase.SshSecretKeyFor(profile), "keyring");

        var useCase = new OpenConnectionUseCase(
            new DatabaseConnectorRegistry([connector]),
            new ConnectionSecretResolver(store),
            new ConnectionTunnelOpener(broker));

        var result = await useCase.ExecuteAsync(draft, new ConnectionSecrets("db"));

        Assert.Equal("keyring", broker.LastRequest?.Secret);

        await result.Session!.DisposeAsync();
    }

    private static (OpenConnectionUseCase UseCase, FakeConnector Connector, FakeSshTunnelBroker Broker) Setup()
    {
        var connector = new FakeConnector();
        var broker = new FakeSshTunnelBroker();

        var useCase = new OpenConnectionUseCase(
            new DatabaseConnectorRegistry([connector]),
            new ConnectionSecretResolver(new InMemorySecretStore()),
            new ConnectionTunnelOpener(broker));

        return (useCase, connector, broker);
    }

    private static SshTunnelSettings Tunnel() => new()
    {
        IsEnabled = true,
        Host = "bastion.internal",
        Port = 22,
        UserName = "alice",
        Authentication = SshAuthenticationMethod.Password
    };

    private static ConnectionDraft Draft(SshTunnelSettings tunnel) => new()
    {
        Id = ConnectionProfileId.New(),
        Name = "sqlforge-test",
        Environment = EnvironmentTag.Local,
        Driver = DatabaseDriver.SqlServer,
        Host = "db.internal",
        Port = 1433,
        Database = "sales_db",
        UserName = "analyst_ro",
        Authentication = AuthenticationMethod.Password,
        StoreSecretInKeyring = false,
        Tls = TlsMode.Require,
        AccessMode = AccessMode.ReadWrite,
        Tunnel = tunnel
    };
}
