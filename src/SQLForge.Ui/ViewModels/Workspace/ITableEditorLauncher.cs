using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// オブジェクトエクスプローラーの右クリックから編集グリッドへつなぐ口。
/// <see cref="IQueryLauncher"/> と同じ考えで、ツリーは相手のテーブルだけを知っていればよい。
/// </summary>
public interface ITableEditorLauncher
{
    /// <summary>そのテーブルの先頭 N 行を編集グリッドで開く。</summary>
    void OpenTableEditor(DatabaseName database, SchemaName schema, string table);
}
