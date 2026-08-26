using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// オブジェクトエクスプローラーの右クリックからスキーマの編集画面へつなぐ口。
/// スキーマそのものはカタログのものだが、決められるのが所有者だけなので
/// 扱いはセキュリティの側にそろえる（SSMS もスキーマを [セキュリティ] の下に置く）。
/// </summary>
public interface ISchemaEditor
{
    /// <summary>新しいスキーマのダイアログを開く。追加されたら true。</summary>
    Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database);

    /// <summary>既存のスキーマのプロパティを開く。変更されたら true。</summary>
    Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, SchemaDescriptor schema);

    /// <summary>確認のうえ削除する。削除されたら true。</summary>
    Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, SchemaDescriptor schema);
}
