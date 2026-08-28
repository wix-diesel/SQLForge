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
    IQueryLauncher? Query = null)
{
    /// <summary>
    /// 右クリックの「先頭 N 行を編集」の行き先。無ければツリーにそのメニューを出さない。
    /// </summary>
    public ITableEditorLauncher? TableEditor { get; init; }

    /// <summary>
    /// セキュリティ（ユーザー）の一式。無ければツリーに「セキュリティ」の枝を出さない。
    /// カタログだけを組みたいときに、ユーザーの読み取り権限まで要求しないための逃げ道でもある。
    /// </summary>
    public DatabaseSecurityContext? Security { get; init; }

    /// <summary>
    /// サーバー スコープのセキュリティ（ログイン）の一式。
    /// 無ければツリーのサーバー直下に「セキュリティ」の枝を出さない。
    /// </summary>
    public ServerSecurityContext? ServerSecurity { get; init; }

    /// <summary>
    /// 右クリックの「フィルターの設定」の行き先。無ければツリーに絞り込みのメニューを出さない
    /// （ツリーだけを組むときに、ダイアログまで用意させないための逃げ道でもある）。
    /// </summary>
    public IObjectFilterEditor? FilterEditor { get; init; }

    /// <summary>
    /// 右クリックの「接続解除」の行き先。メインウィンドウのビューモデルは
    /// このコンテキストを組んだあとにしか作れないので、あとから差す。
    /// </summary>
    public IConnectionLauncher? ConnectionLauncher { get; set; }
}
