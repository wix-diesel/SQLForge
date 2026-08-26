using SQLForge.Application.Abstractions;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// オブジェクトエクスプローラーの右クリックからログインの編集画面へつなぐ口。
///
/// ツリーは「どのログインをどうしたいか」だけを知っていればよく、
/// ダイアログを出すのも、失敗をどう見せるのかも受け取った側の責任にしておく
/// （<see cref="IDatabaseUserEditor"/> と同じ考え方）。
/// </summary>
public interface IServerLoginEditor
{
    /// <summary>新しいログインのダイアログを開く。追加されたら true。</summary>
    Task<bool> CreateAsync(IDatabaseSession session);

    /// <summary>既存のログインのプロパティを開く。変更されたら true。</summary>
    Task<bool> EditAsync(IDatabaseSession session, ServerLoginDescriptor login);

    /// <summary>確認のうえ削除する。削除されたら true。</summary>
    Task<bool> DeleteAsync(IDatabaseSession session, ServerLoginDescriptor login);
}
