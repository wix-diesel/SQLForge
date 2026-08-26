using SQLForge.Application.Abstractions;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// オブジェクトエクスプローラーの右クリックからサーバー ロールの編集画面へつなぐ口。
/// 考え方は <see cref="IServerLoginEditor"/> と同じ。
/// </summary>
public interface IServerRoleEditor
{
    /// <summary>新しいロールのダイアログを開く。追加されたら true。</summary>
    Task<bool> CreateAsync(IDatabaseSession session);

    /// <summary>既存のロールのプロパティを開く。変更されたら true。</summary>
    Task<bool> EditAsync(IDatabaseSession session, ServerRoleDescriptor role);

    /// <summary>確認のうえ削除する。削除されたら true。</summary>
    Task<bool> DeleteAsync(IDatabaseSession session, ServerRoleDescriptor role);
}
