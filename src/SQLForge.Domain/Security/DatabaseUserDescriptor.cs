using SQLForge.Domain.Catalog;

namespace SQLForge.Domain.Security;

/// <summary>
/// データベース内のユーザー 1 件。SSMS の [データベース] → [セキュリティ] → [ユーザー] に並ぶもの。
/// </summary>
/// <param name="Name">ユーザー名。</param>
/// <param name="Type">ユーザーの種類。</param>
/// <param name="LoginName">対応づいたサーバー ログイン。持たない、または読めないときは null。</param>
/// <param name="DefaultSchema">既定のスキーマ。指定が無ければ null（サーバーが dbo を当てる）。</param>
/// <param name="IsSystem">エンジンが用意したユーザー（dbo / guest / sys など）。編集させない。</param>
public sealed record DatabaseUserDescriptor(
    DatabaseUserName Name,
    DatabaseUserType Type,
    string? LoginName = null,
    SchemaName? DefaultSchema = null,
    bool IsSystem = false)
{
    /// <summary>所属しているデータベース ロール。public はすべてのユーザーが持つので含めない。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>この版で編集・削除してよいか。</summary>
    public bool IsEditable => !IsSystem && Type.IsEditable();
}
