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
    ListDatabaseUsersUseCase databaseUsers,
    IDatabaseUserEditor userEditor)
{
    public MainWindowViewModel Create(IDatabaseSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // 作業領域を先に組む。ツリーの右クリックはここへ渡すので、
        // ツリーより先に居てもらう必要がある。
        var query = new QueryEditorViewModel(session, queries);
        var tableEditor = new TableEditorViewModel(session, editTableRows, updateTableCell);

        var catalog = new CatalogContext(
            session, databases, schemas, tables, columns, storedProcedures, storedProcedureParameters, query)
        {
            TableEditor = tableEditor,
            Security = new DatabaseSecurityContext(databaseUsers, userEditor)
        };

        return new MainWindowViewModel(session, platform, catalog, query, tableEditor);
    }
}
