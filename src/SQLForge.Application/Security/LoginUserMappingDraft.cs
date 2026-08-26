using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ログインのプロパティ ダイアログ「ユーザー マッピング」の 1 行。
/// データベースごとに 1 行あり、チェックが付いていない行は「そのデータベースには居ない」を表す。
///
/// <see cref="LoginUserMapping"/> は「居る」ことしか表せないので、
/// 外すという操作を運べるこの器で受け渡す。
/// </summary>
public sealed record LoginUserMappingDraft
{
    /// <summary>対応づけの相手になるデータベース。</summary>
    public required string Database { get; init; }

    /// <summary>このデータベースにユーザーを持つか。SSMS の「マップ」列にあたる。</summary>
    public bool IsMapped { get; init; }

    /// <summary>そのデータベースの中でのユーザー名。空ならログイン名をそのまま使う。</summary>
    public required string UserName { get; init; }

    /// <summary>未指定なら空文字。サーバーが dbo を当てる。</summary>
    public required string DefaultSchema { get; init; }

    /// <summary>そのデータベースで所属させるロール。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>まだ対応づいていないデータベースの行。</summary>
    public static LoginUserMappingDraft Unmapped(string database) =>
        new()
        {
            Database = database,
            IsMapped = false,
            UserName = string.Empty,
            DefaultSchema = string.Empty
        };

    public static LoginUserMappingDraft FromMapping(LoginUserMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return new LoginUserMappingDraft
        {
            Database = mapping.Database.Value,
            IsMapped = true,
            UserName = mapping.User.Value,
            DefaultSchema = mapping.DefaultSchema?.Value ?? string.Empty,
            Roles = mapping.Roles
        };
    }

    /// <summary>
    /// 検証を通ったあとにだけ呼ぶこと。ユーザー名が空ならログイン名をそのまま使う
    /// （SSMS もチェックを付けた直後はログイン名を初期値として入れる）。
    /// </summary>
    public LoginUserMapping ToMapping(string loginName)
    {
        var user = UserName.Trim();
        var schema = DefaultSchema.Trim();

        return new LoginUserMapping(
            new DatabaseName(Database),
            new DatabaseUserName(user.Length > 0 ? user : loginName.Trim()),
            schema.Length > 0 ? new SchemaName(schema) : null)
        {
            Roles = Roles
        };
    }
}
