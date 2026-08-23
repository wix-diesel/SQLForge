using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Connections.SqlServer;
using SQLForge.Infrastructure.Platform;
using SQLForge.Infrastructure.Security;
using SQLForge.Ui.ViewModels;

namespace SQLForge.Ui.Composition;

/// <summary>
/// 合成ルート。オニオンアーキテクチャで外側の実装（Infrastructure）を
/// 内側のポート（Application）へ差し込むのはここだけ。
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build() => Configure(new ServiceCollection()).BuildServiceProvider();

    public static IServiceCollection Configure(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AddInfrastructure(services);
        AddUseCases(services);
        AddViewModels(services);

        return services;
    }

    private static void AddInfrastructure(IServiceCollection services)
    {
        // DBMS を増やすときに触るのはこの 1 行の並びだけ。
        // IDatabaseConnector を足せば、台帳・接続テスト・接続がそのまま新しいドライバーを拾う。
        services.AddSingleton<IDatabaseConnector, SqlServerConnector>();
        services.AddSingleton<IDatabaseConnectorRegistry, DatabaseConnectorRegistry>();
        services.AddSingleton<IConnectionProbe, DriverConnectionProbe>();

        // 保存済み接続とキーリングは、まだ差し替え前提の実装。
        services.AddSingleton<IConnectionProfileRepository, InMemoryConnectionProfileRepository>();
        services.AddSingleton<ISecretStore, InMemorySecretStore>();
        services.AddSingleton<IPlatformProfile, PlatformProfile>();
    }

    private static void AddUseCases(IServiceCollection services)
    {
        services.AddSingleton<ConnectionSecretResolver>();
        services.AddTransient<ListSavedConnectionsUseCase>();
        services.AddTransient<TestConnectionUseCase>();
        services.AddTransient<SaveConnectionUseCase>();
        services.AddTransient<OpenConnectionUseCase>();

        services.AddSingleton<ListDatabasesUseCase>();
        services.AddSingleton<ListSchemasUseCase>();
        services.AddSingleton<ListTablesUseCase>();
    }

    private static void AddViewModels(IServiceCollection services)
    {
        services.AddTransient<SavedConnectionsViewModel>();
        services.AddTransient<ConnectDialogViewModel>();

        // メインウィンドウは開いたセッションを渡して組み立てるので、工場ごしに作る。
        services.AddSingleton<MainWindowViewModelFactory>();
    }
}
