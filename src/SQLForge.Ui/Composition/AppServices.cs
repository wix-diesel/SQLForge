using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Infrastructure.Connections;
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
        // どれも実際の DB・キーリングには触れない差し替え用の実装。
        services.AddSingleton<IConnectionProfileRepository, InMemoryConnectionProfileRepository>();
        services.AddSingleton<IConnectionProbe, SimulatedConnectionProbe>();
        services.AddSingleton<ISecretStore, InMemorySecretStore>();
        services.AddSingleton<IPlatformProfile, PlatformProfile>();
    }

    private static void AddUseCases(IServiceCollection services)
    {
        services.AddTransient<ListSavedConnectionsUseCase>();
        services.AddTransient<TestConnectionUseCase>();
        services.AddTransient<SaveConnectionUseCase>();
        services.AddTransient<OpenConnectionUseCase>();
    }

    private static void AddViewModels(IServiceCollection services)
    {
        services.AddTransient<SavedConnectionsViewModel>();
        services.AddTransient<ConnectDialogViewModel>();
    }
}
