using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Connections;
using SQLForge.Application.Editing;
using SQLForge.Application.Query;
using SQLForge.Application.Security;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.PostgreSql;
using SQLForge.Infrastructure.SqlServer;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.ViewModels.Workspace;
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
        services.AddSingleton<IDatabaseConnector, PostgreSqlConnector>();
        services.AddSingleton<IDatabaseConnectorRegistry, DatabaseConnectorRegistry>();
        services.AddSingleton<IConnectionProbe, DriverConnectionProbe>();

        // SSH の踏み台ごしの経路。DBMS にも OS にも依らないので共通のインフラに置く。
        services.AddSingleton<ISshTunnelBroker, SshTunnelBroker>();

        // OS ごとの体裁も OS ごとに別プロジェクト（SQLForge.Infrastructure.<OS>）へ置き、
        // 実行中の OS のものだけをここで差し込む。並びは PlatformProfiles にある。
        services.AddSingleton(_ => PlatformProfiles.ForCurrentHost());

        // 保存済み接続は OS ごとの設定ディレクトリ（ProfileDirectory）の TOML に置く。
        services.AddSingleton<IConnectionProfileRepository, TomlConnectionProfileRepository>();

        // 書き出し・取り込みのファイルも同じ TOML の形。置き場所は利用者が選ぶ。
        services.AddSingleton<IConnectionArchive, TomlConnectionArchive>();

        // パスワードは OS のキーリングへ預ける。こちらも実装は OS ごとの
        // 別プロジェクトにあり、実行中の OS のものを SecretStores が選ぶ。
        services.AddSingleton(_ => SecretStores.ForCurrentHost());
    }

    private static void AddUseCases(IServiceCollection services)
    {
        services.AddSingleton<ConnectionSecretResolver>();
        services.AddSingleton<ConnectionTunnelOpener>();
        services.AddTransient<ListSavedConnectionsUseCase>();
        services.AddTransient<TestConnectionUseCase>();
        services.AddTransient<SaveConnectionUseCase>();
        services.AddTransient<OpenConnectionUseCase>();
        services.AddTransient<DeleteConnectionUseCase>();
        services.AddTransient<ExportConnectionsUseCase>();
        services.AddTransient<ImportConnectionsUseCase>();

        services.AddSingleton<ListDatabasesUseCase>();
        services.AddSingleton<ListSchemasUseCase>();
        services.AddSingleton<ListTablesUseCase>();
        services.AddSingleton<ListColumnsUseCase>();
        services.AddSingleton<ListStoredProceduresUseCase>();
        services.AddSingleton<ListStoredProcedureParametersUseCase>();
        services.AddSingleton<ExecuteQueryUseCase>();

        services.AddSingleton<EditTableRowsUseCase>();
        services.AddSingleton<UpdateTableCellUseCase>();
        services.AddSingleton<InsertTableRowUseCase>();
        services.AddSingleton<DeleteTableRowUseCase>();

        services.AddSingleton<ListDatabaseUsersUseCase>();
        services.AddSingleton<ListDatabaseRolesUseCase>();
        services.AddSingleton<SaveDatabaseUserUseCase>();
        services.AddSingleton<DropDatabaseUserUseCase>();
        services.AddSingleton<SaveDatabaseRoleUseCase>();
        services.AddSingleton<DropDatabaseRoleUseCase>();

        services.AddSingleton<ListServerLoginsUseCase>();
        services.AddSingleton<ListServerRolesUseCase>();
        services.AddSingleton<SaveServerLoginUseCase>();
        services.AddSingleton<DropServerLoginUseCase>();
        services.AddSingleton<SaveServerRoleUseCase>();
        services.AddSingleton<DropServerRoleUseCase>();

        services.AddSingleton<ListLoginUserMappingsUseCase>();

        services.AddSingleton<SaveSchemaUseCase>();
        services.AddSingleton<DropSchemaUseCase>();

        services.AddSingleton<ListPermissionsUseCase>();
        services.AddSingleton<ListSecurablesUseCase>();
        services.AddSingleton<SavePermissionsUseCase>();
    }

    private static void AddViewModels(IServiceCollection services)
    {
        services.AddTransient<SavedConnectionsViewModel>();
        services.AddTransient<ConnectDialogViewModel>();

        // 左ペインの削除・書き出し・取り込みで出すダイアログ。親は接続ダイアログで、
        // 接続解除のたびに開き直すので、Owner を差し替えられるよう 1 つを共有する。
        services.AddSingleton<SavedConnectionDialogService>();
        services.AddSingleton<ISavedConnectionPrompt>(provider =>
            provider.GetRequiredService<SavedConnectionDialogService>());

        // 「参照…」のファイル選択も、親ウィンドウを持っている同じサービスから借りる。
        services.AddSingleton<IConnectionFilePrompt>(provider =>
            provider.GetRequiredService<SavedConnectionDialogService>());

        // ユーザーの編集ダイアログはメインウィンドウの上にモーダルで出すので、
        // 親ウィンドウが決まったところ（App）で Owner を差せるよう 1 つを共有する。
        services.AddSingleton<DatabaseUserDialogService>();
        services.AddSingleton<IDatabaseUserEditor>(provider =>
            provider.GetRequiredService<DatabaseUserDialogService>());

        // ログインの編集ダイアログも同じ理由で 1 つを共有する。
        services.AddSingleton<ServerLoginDialogService>();
        services.AddSingleton<IServerLoginEditor>(provider =>
            provider.GetRequiredService<ServerLoginDialogService>());

        // ロールとスキーマの編集ダイアログも同じ。
        services.AddSingleton<DatabaseRoleDialogService>();
        services.AddSingleton<IDatabaseRoleEditor>(provider =>
            provider.GetRequiredService<DatabaseRoleDialogService>());

        services.AddSingleton<ServerRoleDialogService>();
        services.AddSingleton<IServerRoleEditor>(provider =>
            provider.GetRequiredService<ServerRoleDialogService>());

        services.AddSingleton<SchemaDialogService>();
        services.AddSingleton<ISchemaEditor>(provider =>
            provider.GetRequiredService<SchemaDialogService>());

        // ツリーの「フィルターの設定」も、メインウィンドウの上に出すので同じ扱い。
        services.AddSingleton<ObjectFilterDialogService>();
        services.AddSingleton<IObjectFilterEditor>(provider =>
            provider.GetRequiredService<ObjectFilterDialogService>());

        // 編集グリッドの「行の削除」の確認も、同じ理由で 1 つを共有する。
        services.AddSingleton<TableRowDeleteDialogService>();
        services.AddSingleton<IRowDeletionPrompt>(provider =>
            provider.GetRequiredService<TableRowDeleteDialogService>());

        // メインウィンドウは開いたセッションを渡して組み立てるので、工場ごしに作る。
        services.AddSingleton<MainWindowViewModelFactory>();
    }
}
