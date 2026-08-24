using System.Text;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// ユーザーの追加・編集・削除の文面。ユーザー名・ログイン名・スキーマ名・ロール名は
/// どれも識別子でパラメータにできないので、<see cref="SqlServerIdentifier.Quote"/> を通して埋める。
/// </summary>
internal static class SqlServerSecurityStatements
{
    public static IReadOnlyList<string> Create(DatabaseUserDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var name = Quote(definition.Name.Value);
        var create = new StringBuilder("CREATE USER ").Append(name);

        // 種類とログインの組み合わせは DatabaseUserDefinition が保証しているので、
        // ここでは相手がいるかどうかだけを見ればよい。
        create.Append(definition.LoginName is { } login
            ? $" FOR LOGIN {Quote(login)}"
            : " WITHOUT LOGIN");

        if (definition.DefaultSchema is { } schema)
        {
            create.Append($" WITH DEFAULT_SCHEMA = {Quote(schema.Value)}");
        }

        create.Append(';');

        var statements = new List<string> { create.ToString() };
        statements.AddRange(definition.Roles.Select(role => AddMember(role, name)));

        return statements;
    }

    public static IReadOnlyList<string> Alter(DatabaseUserDescriptor original, DatabaseUserDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        var statements = new List<string>();
        var clauses = AlterClauses(original, definition);

        if (clauses.Count > 0)
        {
            statements.Add($"ALTER USER {Quote(original.Name.Value)} WITH {string.Join(", ", clauses)};");
        }

        // 名前を変えたあとのロール操作は新しい名前で行う（ALTER USER を先に流すため）。
        var name = Quote(definition.Name.Value);

        statements.AddRange(definition.Roles
            .Except(original.Roles, StringComparer.OrdinalIgnoreCase)
            .Select(role => AddMember(role, name)));

        statements.AddRange(original.Roles
            .Except(definition.Roles, StringComparer.OrdinalIgnoreCase)
            .Select(role => $"ALTER ROLE {Quote(role)} DROP MEMBER {name};"));

        return statements;
    }

    public static string Drop(DatabaseUserName user) => $"DROP USER {Quote(user.Value)};";

    /// <summary>
    /// 実際に変わったところだけを並べる。変わっていない項目まで出すと、
    /// ログインを付け替える権限が無いだけの利用者が既定のスキーマも直せなくなる。
    /// </summary>
    private static List<string> AlterClauses(DatabaseUserDescriptor original, DatabaseUserDefinition definition)
    {
        var clauses = new List<string>();

        if (!string.Equals(original.Name.Value, definition.Name.Value, StringComparison.Ordinal))
        {
            clauses.Add($"NAME = {Quote(definition.Name.Value)}");
        }

        var schema = definition.DefaultSchema?.Value;

        if (!string.Equals(original.DefaultSchema?.Value, schema, StringComparison.Ordinal))
        {
            // 既定のスキーマを外すときは NULL を明示する（省くと今の値が残る）。
            clauses.Add($"DEFAULT_SCHEMA = {(schema is null ? "NULL" : Quote(schema))}");
        }

        if (definition.LoginName is { } login
            && !string.Equals(original.LoginName, login, StringComparison.OrdinalIgnoreCase))
        {
            clauses.Add($"LOGIN = {Quote(login)}");
        }

        return clauses;
    }

    private static string AddMember(string role, string quotedUser) =>
        $"ALTER ROLE {Quote(role)} ADD MEMBER {quotedUser};";

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
