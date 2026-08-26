using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Connections;
using SQLForge.Application.Editing;
using SQLForge.Application.Query;
using SQLForge.Application.Security;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.SqlServer;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.Views;

namespace SQLForge.Ui.Composition;

/// <summary>
/// 合成ルート。オニオンアーキテクチャで外側の実装（Infrastructure）を
/// 内側のポート（Application）へ差し込むのはここだけ。
///
/// ドライバーと OS の具体型を知ってよいのもここだけで、その代わりに
/// SQLForge.Ui は DBMS ごと・OS ごとのプロジェクトを参照する。
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
        // DBMS を増やすときに触るのはこの 1 行の並びだけ。ドライバーは DBMS ごとに
        // 別プロジェクト（SQLForge.Infrastructure.<DBMS>）に置き、ここで差し込む。
        // 台帳・接続テスト・接続は登録された IDatabaseConnector をそのまま拾う。
        services.AddSingleton<IDatabaseConnector, SqlServerConnector>();
        services.AddSingleton<IDatabaseConnectorRegistry, DatabaseConnectorRegistry>();
        services.AddSingleton<IConnectionProbe, DriverConnectionProbe>();

        // OS ごとの体裁も OS ごとに別プロジェクト（SQLForge.Infrastructure.<OS>）へ置き、
        // 実行中の OS のものだけをここで差し込む。並びは PlatformProfiles にある。
        services.AddSingleton(_ => PlatformProfiles.ForCurrentHost());

        // 保存済み接続は OS ごとの設定ディレクトリ（ProfileDirectory）の TOML に置く。
        services.AddSingleton<IConnectionProfileRepository, TomlConnectionProfileRepository>();

        // パスワードは OS のキーリングへ預ける。こちらも実装は OS ごとの
        // 別プロジェクトにあり、実行中の OS のものを SecretStores が選ぶ。
        services.AddSingleton(_ => SecretStores.ForCurrentHost());
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
        services.AddSingleton<ListColumnsUseCase>();
        services.AddSingleton<ListStoredProceduresUseCase>();
        services.AddSingleton<ListStoredProcedureParametersUseCase>();
        services.AddSingleton<ExecuteQueryUseCase>();

        services.AddSingleton<EditTableRowsUseCase>();
        services.AddSingleton<UpdateTableCellUseCase>();

        services.AddSingleton<ListDatabaseUsersUseCase>();
        services.AddSingleton<ListDatabaseRolesUseCase>();
        services.AddSingleton<SaveDatabaseUserUseCase>();
        services.AddSingleton<DropDatabaseUserUseCase>();
    }

    private static void AddViewModels(IServiceCollection services)
    {
        services.AddTransient<SavedConnectionsViewModel>();
        services.AddTransient<ConnectDialogViewModel>();

        // ユーザーの編集ダイアログはメインウィンドウの上にモーダルで出すので、
        // 親ウィンドウが決まったところ（App）で Owner を差せるよう 1 つを共有する。
        services.AddSingleton<DatabaseUserDialogService>();
        services.AddSingleton<IDatabaseUserEditor>(provider =>
            provider.GetRequiredService<DatabaseUserDialogService>());

        // メインウィンドウは開いたセッションを渡して組み立てるので、工場ごしに作る。
        services.AddSingleton<MainWindowViewModelFactory>();
    }
}
