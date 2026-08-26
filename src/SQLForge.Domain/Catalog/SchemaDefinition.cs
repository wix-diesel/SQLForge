namespace SQLForge.Domain.Catalog;

/// <summary>
/// これから作る（あるいは作り替える）スキーマのあるべき姿。
/// 検証を通った入力だけがこの形になり、ドライバーはこれを文面に写す。
///
/// SSMS の「スキーマ - 新規作成」に合わせ、決められるのは名前と所有者だけ。
/// </summary>
/// <param name="Name">スキーマ名。</param>
/// <param name="Owner">
/// 所有者（データベース ユーザーまたはデータベース ロール）。
/// 指定しないなら null で、サーバーが作成した利用者を当てる。
/// </param>
public sealed record SchemaDefinition(SchemaName Name, string? Owner = null);
