using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using SQLForge.Ui.ViewModels;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>左ペインで保存済み接続を選んだときの自動接続。</summary>
public class ConnectDialogViewModelTests
{
    [Fact]
    public async Task 起動しただけでは接続しない()
    {
        var (dialog, connector, _) = Setup();
        IDatabaseSession? opened = null;
        dialog.ConnectionEstablished += (_, session) => opened = session;

        await dialog.InitializeAsync();

        Assert.Equal("prod-sales", dialog.Form.Name);
        Assert.Equal(0, connector.ConnectCount);
        Assert.Null(opened);
    }

    [Fact]
    public async Task 左ペインで選ぶと預けてあるパスワードで接続する()
    {
        var (dialog, connector, store) = Setup();
        await dialog.InitializeAsync();
        var item = dialog.SavedConnections.Entries.OfType<SavedConnectionItemViewModel>().First();
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(item.Profile), "s3cret");

        IDatabaseSession? opened = null;
        dialog.ConnectionEstablished += (_, session) => opened = session;

        dialog.SavedConnections.Activate(item);
        await WaitUntil(() => opened is not null);

        Assert.Equal("s3cret", connector.LastRequest?.Secret);
        Assert.Equal(item.Profile.Id, opened!.Profile.Id);
        await opened.DisposeAsync();
    }

    [Fact]
    public async Task パスワードが預けられていなければ入力を促すだけ()
    {
        var (dialog, connector, _) = Setup();
        await dialog.InitializeAsync();
        var item = dialog.SavedConnections.Entries.OfType<SavedConnectionItemViewModel>().Last();

        dialog.SavedConnections.Activate(item);
        await WaitUntil(() => dialog.HasStatus);

        Assert.Equal(0, connector.ConnectCount);
        Assert.Equal(item.Profile.Name, dialog.Form.Name);
        Assert.Contains("パスワード", dialog.StatusHeadline, StringComparison.Ordinal);
    }

    private static (ConnectDialogViewModel Dialog, FakeConnector Connector, InMemorySecretStore Store) Setup()
    {
        var repository = InMemoryConnectionProfileRepository.With(
            [Profile("prod-sales", EnvironmentTag.Production), Profile("local-dev", EnvironmentTag.Local)]);
        var store = new InMemorySecretStore();
        var resolver = new ConnectionSecretResolver(store);
        var connector = new FakeConnector();
        var registry = new DatabaseConnectorRegistry([connector]);

        var dialog = new ConnectDialogViewModel(
            new SavedConnectionsViewModel(new ListSavedConnectionsUseCase(repository)),
            new TestConnectionUseCase(new DriverConnectionProbe(registry), resolver),
            new SaveConnectionUseCase(repository, store),
            new OpenConnectionUseCase(registry, resolver),
            store);

        return (dialog, connector, store);
    }

    private static ConnectionProfile Profile(string name, EnvironmentTag environment) =>
        new(ConnectionProfileId.New(),
            name,
            environment,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db"),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: true),
            ConnectionProfile.DefaultAccessModeFor(environment));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), "期待した状態になりませんでした。");
    }
}
