using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ユーザーのプロパティ ダイアログで編集中の入力値。
/// エンティティ（<see cref="DatabaseUserDefinition"/>）は常に妥当である前提なので、
/// まだ妥当とは限らない入力はこの器で受け渡す。
/// </summary>
public sealed record DatabaseUserDraft
{
    /// <summary>編集前の姿。新しく作るなら null。</summary>
    public DatabaseUserDescriptor? Original { get; init; }

    public required string Name { get; init; }

    public required DatabaseUserType Type { get; init; }

    /// <summary>ログインを取らない種類では使わない。</summary>
    public required string LoginName { get; init; }

    /// <summary>未指定なら空文字。サーバーが dbo を当てる。</summary>
    public required string DefaultSchema { get; init; }

    /// <summary>所属させるデータベース ロール。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool IsNew => Original is null;

    /// <summary>新規作成の初期値。SSMS と同じくログインありの SQL ユーザーから始める。</summary>
    public static DatabaseUserDraft ForNewUser() =>
        new()
        {
            Name = string.Empty,
            Type = DatabaseUserType.SqlUserWithLogin,
            LoginName = string.Empty,
            DefaultSchema = string.Empty
        };

    public static DatabaseUserDraft FromDescriptor(DatabaseUserDescriptor user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new DatabaseUserDraft
        {
            Original = user,
            Name = user.Name.Value,
            Type = user.Type,
            LoginName = user.LoginName ?? string.Empty,
            DefaultSchema = user.DefaultSchema?.Value ?? string.Empty,
            Roles = user.Roles
        };
    }

    /// <summary>検証を通ったあとにだけ呼ぶこと。</summary>
    public DatabaseUserDefinition ToDefinition()
    {
        var login = LoginName.Trim();
        var schema = DefaultSchema.Trim();

        return new DatabaseUserDefinition(
            new DatabaseUserName(Name.Trim()),
            Type,
            // 種類を切り替えたあとに前の入力が残っていても、文面へは持ち出さない。
            Type.RequiresLogin() && login.Length > 0 ? login : null,
            schema.Length > 0 ? new SchemaName(schema) : null)
        {
            Roles = Roles
        };
    }
}
