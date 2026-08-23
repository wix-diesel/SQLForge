using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// オブジェクトエクスプローラーの右クリックから作業領域へつなぐ口。
///
/// ツリーは「どのデータベースを相手にするか」だけを知っていればよく、
/// 文面の組み立ても実行も受け取った側の責任にしておく（ツリーのノードがクエリの
/// 都合を持ち始めると、ノードの種類を増やすたびに同じ話が増える）。
/// </summary>
public interface IQueryLauncher
{
    /// <summary>空のエディタをそのデータベース向けに開く。</summary>
    void OpenNewQuery(DatabaseName database);

    /// <summary>エディタをそのデータベース向けに開き、渡した文面をそのまま実行する。</summary>
    void OpenAndRunQuery(DatabaseName database, string sql);
}
