using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// オブジェクトエクスプローラーの右クリックからユーザーの編集画面へつなぐ口。
///
/// ツリーは「どのユーザーをどうしたいか」だけを知っていればよく、
/// ダイアログを出すのも、失敗をどう見せるのかも受け取った側の責任にしておく
/// （<see cref="Workspace.IQueryLauncher"/> と同じ考え方）。
/// </summary>
public interface IDatabaseUserEditor
{
    /// <summary>新しいユーザーのダイアログを開く。追加されたら true。</summary>
    Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database);

    /// <summary>既存のユーザーのプロパティを開く。変更されたら true。</summary>
    Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, DatabaseUserDescriptor user);

    /// <summary>確認のうえ削除する。削除されたら true。</summary>
    Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, DatabaseUserDescriptor user);
}
