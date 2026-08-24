using SQLForge.Domain.Catalog;

namespace SQLForge.Domain.Security;

/// <summary>
/// これから作る（あるいは作り替える）ユーザーのあるべき姿。
/// 検証を通った入力だけがこの形になり、ドライバーはこれを文面に写す。
/// </summary>
/// <param name="Name">ユーザー名。</param>
/// <param name="Type">ユーザーの種類。</param>
/// <param name="LoginName">対応づけるログイン。ログインを取らない種類では null。</param>
/// <param name="DefaultSchema">既定のスキーマ。指定しないなら null。</param>
public sealed record DatabaseUserDefinition(
    DatabaseUserName Name,
    DatabaseUserType Type,
    string? LoginName = null,
    SchemaName? DefaultSchema = null)
{
    /// <summary>所属させるデータベース ロール。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
