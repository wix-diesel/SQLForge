using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.PostgreSql;
using SQLForge.Infrastructure.Security;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 入っているドライバーと入っていないドライバーの切り分け。
/// 未対応の DBMS を成功に見せかけないことがここの主旨。
/// </summary>
public class DriverSupportTests
{
    [Fact]
    public void 台帳は登録されたドライバーだけを対応済みとする()
    {
        var registry = NewRegistry();

        Assert.True(registry.Supports(DatabaseDriver.SqlServer));
        Assert.True(registry.Supports(DatabaseDriver.PostgreSql));
        Assert.False(registry.Supports(DatabaseDriver.MySql));

        // 並びは接続ダイアログのドライバー一覧と同じ順序になる。
        Assert.Equal([DatabaseDriver.SqlServer, DatabaseDriver.PostgreSql], registry.SupportedDrivers);
    }

    [Fact]
    public async Task 未対応ドライバーの接続テストは理由付きで失敗する()
    {
        var probe = new DriverConnectionProbe(NewRegistry());
        var profile = ProfileFor(DatabaseDriver.ClickHouse);

        var result = await probe.ProbeAsync(new ConnectionRequest(profile, null));

        Assert.False(result.Succeeded);
        Assert.Contains("ClickHouse", result.Detail, StringComparison.Ordinal);
        Assert.Contains("SQL Server", result.Detail, StringComparison.Ordinal);
        Assert.Contains("PostgreSQL", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSQLでもドライバーが受け付けない設定は接続前に理由を返す()
    {
        // 対応済みのドライバーでも、写せない指定は接続を試す前に分かる。
        var useCase = new OpenConnectionUseCase(NewRegistry(), NewSecretResolver(), NewTunnelOpener());
        var profile = ProfileFor(DatabaseDriver.PostgreSql);
        var draft = ConnectionDraft.FromProfile(profile) with { Authentication = AuthenticationMethod.Certificate };

        var result = await useCase.ExecuteAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        Assert.Contains("証明書", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 未対応ドライバーの接続はセッションを返さない()
    {
        var useCase = new OpenConnectionUseCase(NewRegistry(), NewSecretResolver(), NewTunnelOpener());
        var draft = ConnectionDraft.FromProfile(ProfileFor(DatabaseDriver.MySql));

        var result = await useCase.ExecuteAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        Assert.Contains("MySQL", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 入力が不正なら接続する前に弾く()
    {
        var useCase = new OpenConnectionUseCase(NewRegistry(), NewSecretResolver(), NewTunnelOpener());
        var draft = ConnectionDraft.FromProfile(ProfileFor(DatabaseDriver.SqlServer)) with { Host = string.Empty };

        var result = await useCase.ExecuteAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        Assert.False(result.Validation.IsValid);
    }

    [Fact]
    public async Task ドライバーが受け付けない設定は接続前に理由を返す()
    {
        // 証明書認証は SqlClient の接続文字列に写せないので、接続を試みる前に分かる。
        var useCase = new OpenConnectionUseCase(NewRegistry(), NewSecretResolver(), NewTunnelOpener());
        var profile = ProfileFor(DatabaseDriver.SqlServer);
        var draft = ConnectionDraft.FromProfile(profile) with { Authentication = AuthenticationMethod.Certificate };

        var result = await useCase.ExecuteAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        Assert.Contains("証明書", result.Detail, StringComparison.Ordinal);
    }

    private static DatabaseConnectorRegistry NewRegistry() =>
        new([new SqlServerConnector(), new PostgreSqlConnector()]);

    private static ConnectionSecretResolver NewSecretResolver() => new(new InMemorySecretStore());

    private static ConnectionTunnelOpener NewTunnelOpener() => new(new FakeSshTunnelBroker());

    private static ConnectionProfile ProfileFor(DatabaseDriver driver) =>
        new(ConnectionProfileId.New(),
            "sqlforge-test",
            EnvironmentTag.Local,
            new ConnectionTarget(driver, new ServerAddress("db.internal", driver.DefaultPort), "sales_db"),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: false),
            AccessMode.ReadWrite);
}
