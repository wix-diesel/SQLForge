using SQLForge.Domain.Catalog;

namespace SQLForge.Domain.Security;

/// <summary>
/// これから作る（あるいは作り替える）ユーザーのあるべき姿。
/// 検証を通った入力だけがこの形になり、ドライバーはこれを文面に写す。
///
/// 種類とログインの組み合わせは常に妥当である前提なので、その 2 つは作るときにしか決められない
/// （<c>with</c> で後から差し替えられると、コンストラクタの検査をすり抜けてしまう）。
/// </summary>
public sealed record DatabaseUserDefinition
{
    /// <param name="name">ユーザー名。</param>
    /// <param name="type">ユーザーの種類。</param>
    /// <param name="loginName">対応づけるログイン。ログインを取らない種類では null。</param>
    /// <param name="defaultSchema">既定のスキーマ。指定しないなら null。</param>
    public DatabaseUserDefinition(
        DatabaseUserName name,
        DatabaseUserType type,
        string? loginName = null,
        SchemaName? defaultSchema = null)
    {
        // ログインを取る種類なのに相手が決まっていないと、文面が WITHOUT LOGIN へ倒れて
        // 頼んだのとは別の種類のユーザーが黙って出来上がる。サーバーへ送る前にここで止める。
        if (type.RequiresLogin() && string.IsNullOrWhiteSpace(loginName))
        {
            throw new ArgumentException($"{type.DisplayName()} にはログイン名が要ります。", nameof(loginName));
        }

        Name = name;
        Type = type;
        LoginName = type.RequiresLogin() ? loginName : null;
        DefaultSchema = defaultSchema;
    }

    public DatabaseUserName Name { get; }

    public DatabaseUserType Type { get; }

    /// <summary>対応づけるログイン。ログインを取らない種類では必ず null。</summary>
    public string? LoginName { get; }

    /// <summary>既定のスキーマ。指定しないなら null。</summary>
    public SchemaName? DefaultSchema { get; }

    /// <summary>所属させるデータベース ロール。種類とは関わらないので後から差し替えてよい。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
