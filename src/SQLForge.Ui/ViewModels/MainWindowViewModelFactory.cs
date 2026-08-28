using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Editing;
using SQLForge.Application.Query;
using SQLForge.Application.Security;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.ViewModels.Workspace;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// メインウィンドウのビューモデルを組み立てる。セッションだけは実行時にしか決まらないので、
/// DI から取れる残りをここで束ねておく。
/// </summary>
public sealed class MainWindowViewModelFactory(
    IPlatformProfile platform,
    ListDatabasesUseCase databases,
    ListSchemasUseCase schemas,
    ListTablesUseCase tables,
    ListColumnsUseCase columns,
    ListStoredProceduresUseCase storedProcedures,
    ListStoredProcedureParametersUseCase storedProcedureParameters,
    ExecuteQueryUseCase queries,
    EditTableRowsUseCase editTableRows,
    UpdateTableCellUseCase updateTableCell,
    InsertTableRowUseCase insertTableRow,
    DeleteTableRowUseCase deleteTableRow,
    IRowDeletionPrompt rowDeletionPrompt,
    ListDatabaseUsersUseCase databaseUsers,
    IDatabaseUserEditor userEditor,
    ListDatabaseRolesUseCase databaseRoles,
    IDatabaseRoleEditor databaseRoleEditor,
    ISchemaEditor schemaEditor,
    ListServerLoginsUseCase serverLogins,
    IServerLoginEditor loginEditor,
    ListServerRolesUseCase serverRoles,
    IServerRoleEditor serverRoleEditor,
    IObjectFilterEditor filterEditor)
{
    public MainWindowViewModel Create(IDatabaseSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // 補完はカタログを読むので、接続 1 本につき 1 つの覚え書き（SchemaCache）を持たせる。
        var completion = new SqlCompletionUseCase(new SchemaCache(session, schemas, tables, columns));

        // 作業領域を先に組む。ツリーの右クリックはここへ渡すので、
        // ツリーより先に居てもらう必要がある。
        var query = new QueryEditorViewModel(session, queries, completion);
        var tableEditor = new TableEditorViewModel(
            session, editTableRows, updateTableCell, insertTableRow, deleteTableRow, rowDeletionPrompt);

        var catalog = new CatalogContext(
            session, databases, schemas, tables, columns, storedProcedures, storedProcedureParameters, query)
        {
            TableEditor = tableEditor,
            FilterEditor = filterEditor,
            Security = new DatabaseSecurityContext(databaseUsers, userEditor)
            {
                Roles = databaseRoles,
                RoleEditor = databaseRoleEditor,
                SchemaEditor = schemaEditor
            },
            ServerSecurity = new ServerSecurityContext(serverLogins, loginEditor)
            {
                Roles = serverRoles,
                RoleEditor = serverRoleEditor
            }
        };

        return new MainWindowViewModel(session, platform, catalog, query, tableEditor);
    }
}
