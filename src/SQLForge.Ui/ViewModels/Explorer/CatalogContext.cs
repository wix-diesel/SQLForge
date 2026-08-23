using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Ui.ViewModels.Workspace;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ツリーのノードがカタログを読むのに要る一式。ノードを増やすたびに
/// コンストラクタ引数が増えないよう、まとめて 1 つで渡す。
/// </summary>
/// <param name="Session">開いている接続。</param>
/// <param name="Databases">データベース一覧のユースケース。</param>
/// <param name="Schemas">スキーマ一覧のユースケース。</param>
/// <param name="Tables">テーブル一覧のユースケース。</param>
/// <param name="Columns">カラム定義一覧のユースケース。</param>
/// <param name="StoredProcedures">ストアド プロシージャ一覧のユースケース。</param>
/// <param name="StoredProcedureParameters">ストアド プロシージャのパラメーター一覧のユースケース。</param>
/// <param name="Query">右クリックの「クエリを実行」の行き先。ツリーだけを組むときは無くてよい。</param>
public sealed record CatalogContext(
    IDatabaseSession Session,
    ListDatabasesUseCase Databases,
    ListSchemasUseCase Schemas,
    ListTablesUseCase Tables,
    ListColumnsUseCase Columns,
    ListStoredProceduresUseCase StoredProcedures,
    ListStoredProcedureParametersUseCase StoredProcedureParameters,
    IQueryLauncher? Query = null);
