using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// オブジェクトエクスプローラーの右クリックから作業領域へつなぐ口。
///
/// ツリーは「どのテーブルか」だけを知っていればよく、文面の組み立ても実行も
/// 受け取った側の責任にしておく（ツリーのノードがクエリの都合を持ち始めると、
/// ノードの種類を増やすたびに同じ話が増える）。
/// </summary>
public interface IQueryLauncher
{
    /// <summary>テーブルの中身をのぞく文面を用意してエディタを開く。実行はしない。</summary>
    void OpenTableQuery(DatabaseName database, SchemaName schema, string table);

    /// <summary>空のエディタをそのデータベース向けに開く。</summary>
    void OpenNewQuery(DatabaseName database);
}
