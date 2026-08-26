using SQLForge.Domain.Catalog;

namespace SQLForge.Domain.Security;

/// <summary>
/// ログイン 1 件が、あるデータベースへ持っているユーザーの対応づけ 1 行。
/// SSMS の「ログインのプロパティ」→「ユーザー マッピング」の 1 行にあたる。
///
/// 対応づけがあるということはユーザーが必ず 1 人いる、ということなので、
/// 「マップしていない」状態はこの型では表さない（一覧に出さないことで表す）。
/// </summary>
/// <param name="Database">対応づいたデータベース。</param>
/// <param name="User">そのデータベースの中でのユーザー名。</param>
/// <param name="DefaultSchema">既定のスキーマ。指定が無ければ null（サーバーが dbo を当てる）。</param>
public sealed record LoginUserMapping(
    DatabaseName Database,
    DatabaseUserName User,
    SchemaName? DefaultSchema = null)
{
    /// <summary>そのデータベースで所属しているロール。public はすべてが持つので含めない。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
