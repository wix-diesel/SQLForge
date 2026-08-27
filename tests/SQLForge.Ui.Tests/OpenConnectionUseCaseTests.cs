using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 保存済み接続をそのまま開く経路（左ペインで選んだときの自動接続）。
/// 入力欄を経由しないので、パスワードはキーリングに預けてあるものだけを使う。
/// </summary>
public class OpenConnectionUseCaseTests
{
    [Fact]
    public async Task 預けてあるパスワードで開く()
    {
        var (useCase, store, connector, profile) = Setup();
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(profile), "s3cret");

        var result = await useCase.ExecuteStoredAsync(profile);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Session);
        Assert.Equal("s3cret", connector.LastRequest?.Secret);
        await result.Session!.DisposeAsync();
    }

    [Fact]
    public async Task パスワードが預けられていなければ接続を試みない()
    {
        // 空のパスワードでサーバーを叩いて失敗を出すより、入力を促すほうが分かりやすい。
        var (useCase, _, connector, profile) = Setup();

        var result = await useCase.ExecuteStoredAsync(profile);

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresSecret);
        Assert.Null(result.Session);
        Assert.Equal(0, connector.ConnectCount);
        Assert.Contains("パスワード", result.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OS統合認証の接続はパスワード無しで開く()
    {
        var (useCase, _, connector, profile) = Setup();
        var integrated = new ConnectionProfile(
            profile.Id,
            profile.Name,
            profile.Environment,
            profile.Target,
            new ConnectionCredentials(string.Empty, AuthenticationMethod.Integrated, storeSecretInKeyring: false),
            profile.AccessMode);

        var result = await useCase.ExecuteStoredAsync(integrated);

        Assert.True(result.Succeeded);
        Assert.Null(connector.LastRequest?.Secret);
        await result.Session!.DisposeAsync();
    }

    private static (OpenConnectionUseCase UseCase, InMemorySecretStore Store, FakeConnector Connector, ConnectionProfile Profile) Setup()
    {
        var store = new InMemorySecretStore();
        var connector = new FakeConnector();
        var useCase = new OpenConnectionUseCase(
            new DatabaseConnectorRegistry([connector]),
            new ConnectionSecretResolver(store),
            new ConnectionTunnelOpener(new FakeSshTunnelBroker()));

        var profile = new ConnectionProfile(
            ConnectionProfileId.New(),
            "prod-sales",
            EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db"),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: true),
            AccessMode.ReadOnly);

        return (useCase, store, connector, profile);
    }
}
