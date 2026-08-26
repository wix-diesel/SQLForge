using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// オブジェクトエクスプローラーの右クリックからデータベース ロールの編集画面へつなぐ口。
/// 考え方は <see cref="IDatabaseUserEditor"/> と同じ。
/// </summary>
public interface IDatabaseRoleEditor
{
    /// <summary>新しいロールのダイアログを開く。追加されたら true。</summary>
    Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database);

    /// <summary>既存のロールのプロパティを開く。変更されたら true。</summary>
    Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, DatabaseRoleDescriptor role);

    /// <summary>確認のうえ削除する。削除されたら true。</summary>
    Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, DatabaseRoleDescriptor role);
}
