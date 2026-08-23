using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ツリーのノードがカタログを読むのに要る一式。ノードを増やすたびに
/// コンストラクタ引数が増えないよう、まとめて 1 つで渡す。
/// </summary>
/// <param name="Session">開いている接続。</param>
/// <param name="Databases">データベース一覧のユースケース。</param>
/// <param name="Schemas">スキーマ一覧のユースケース。</param>
/// <param name="Tables">テーブル一覧のユースケース。</param>
public sealed record CatalogContext(
    IDatabaseSession Session,
    ListDatabasesUseCase Databases,
    ListSchemasUseCase Schemas,
    ListTablesUseCase Tables);
