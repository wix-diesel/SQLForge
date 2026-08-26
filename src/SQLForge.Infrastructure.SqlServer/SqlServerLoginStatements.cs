using System.Text;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// ログインの追加・編集・削除の文面。名前・データベース名・ロール名は識別子なので
/// <see cref="SqlServerIdentifier.Quote"/> を、パスワードは値なので
/// <see cref="SqlServerLiteral.Quote"/> を通して埋める。
/// </summary>
internal static class SqlServerLoginStatements
{
    public static IReadOnlyList<string> Create(ServerLoginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var name = Quote(definition.Name.Value);
        var statements = new List<string> { CreateLogin(definition, name) };

        // 無効にして作ることはできない。作ってから落とす（SSMS も同じ順で文面を出す）。
        if (definition.IsDisabled)
        {
            statements.Add($"ALTER LOGIN {name} DISABLE;");
        }

        statements.AddRange(definition.Roles.Select(role => AddMember(role, name)));

        return statements;
    }

    public static IReadOnlyList<string> Alter(ServerLoginDescriptor original, ServerLoginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(definition);

        var name = Quote(original.Name.Value);
        var statements = new List<string>();
        var options = AlterOptions(original, definition);

        // 規則を先に、パスワードを後に流す。順が逆だと、期限の適用を今から入れる編集で
        // MUST_CHANGE がまだ通らず、ポリシーを緩める編集では新しいパスワードが古い規則で弾かれる。
        if (options.Count > 0)
        {
            statements.Add($"ALTER LOGIN {name} WITH {string.Join(", ", options)};");
        }

        // 空欄は「今のまま」。パスワードはサーバーから読めないので、変えるときだけ文面に出す。
        if (definition.Password is { } password)
        {
            statements.Add($"ALTER LOGIN {name} WITH PASSWORD = {Password(password, definition)};");
        }

        if (definition.IsDisabled != original.IsDisabled)
        {
            statements.Add($"ALTER LOGIN {name} {(definition.IsDisabled ? "DISABLE" : "ENABLE")};");
        }

        if (!string.Equals(original.Name.Value, definition.Name.Value, StringComparison.Ordinal))
        {
            statements.Add($"ALTER LOGIN {name} WITH NAME = {Quote(definition.Name.Value)};");
        }

        statements.AddRange(RoleChanges(original, definition));

        return statements;
    }

    public static string Drop(ServerLoginName login) => $"DROP LOGIN {Quote(login.Value)};";

    private static string CreateLogin(ServerLoginDefinition definition, string quotedName)
    {
        var create = new StringBuilder("CREATE LOGIN ").Append(quotedName);

        if (!definition.Type.RequiresPassword())
        {
            // Windows のユーザーとグループの別は SID から決まるので、文面はどちらも同じ。
            create.Append(" FROM WINDOWS");

            if (definition.DefaultDatabase is { } windowsDatabase)
            {
                create.Append($" WITH DEFAULT_DATABASE = {Quote(windowsDatabase.Value)}");
            }

            return create.Append(';').ToString();
        }

        if (definition.Password is not { } password)
        {
            // 空のパスワードで作ると、誰でも入れるログインが黙って出来上がる。ここで止める。
            throw new ArgumentException(
                "SQL Server 認証のログインを作るにはパスワードが要ります。",
                nameof(definition));
        }

        var policy = definition.PasswordPolicy ?? ServerLoginPasswordPolicy.Default;

        var options = new List<string>
        {
            $"PASSWORD = {Password(password, definition)}",
            $"CHECK_POLICY = {OnOff(policy.EnforcePolicy)}",
            $"CHECK_EXPIRATION = {OnOff(policy.EnforceExpiration)}"
        };

        if (definition.DefaultDatabase is { } database)
        {
            options.Add($"DEFAULT_DATABASE = {Quote(database.Value)}");
        }

        return create.Append(" WITH ").Append(string.Join(", ", options)).Append(';').ToString();
    }

    /// <summary>
    /// 実際に変わったところだけを並べる。変わっていない項目まで出すと、
    /// たとえば既定のデータベースを直したいだけの利用者が、規則を変える権限まで問われる。
    /// </summary>
    private static List<string> AlterOptions(ServerLoginDescriptor original, ServerLoginDefinition definition)
    {
        var options = new List<string>();

        // 規則を持つのは SQL Server 認証のログインだけ。持たない相手とは比べようがない。
        if (definition.PasswordPolicy is { } policy && original.PasswordPolicy is { } before)
        {
            if (policy.EnforcePolicy != before.EnforcePolicy)
            {
                options.Add($"CHECK_POLICY = {OnOff(policy.EnforcePolicy)}");
            }

            if (policy.EnforceExpiration != before.EnforceExpiration)
            {
                options.Add($"CHECK_EXPIRATION = {OnOff(policy.EnforceExpiration)}");
            }
        }

        // 既定のデータベースは NULL にできない。空欄は「今のまま」として文面に出さない。
        if (definition.DefaultDatabase is { } database
            && !string.Equals(original.DefaultDatabase?.Value, database.Value, StringComparison.Ordinal))
        {
            options.Add($"DEFAULT_DATABASE = {Quote(database.Value)}");
        }

        return options;
    }

    /// <summary>名前を変えたあとのロール操作は新しい名前で行う（ALTER LOGIN を先に流すため）。</summary>
    private static IEnumerable<string> RoleChanges(ServerLoginDescriptor original, ServerLoginDefinition definition)
    {
        var name = Quote(definition.Name.Value);

        var added = definition.Roles
            .Except(original.Roles, StringComparer.OrdinalIgnoreCase)
            .Select(role => AddMember(role, name));

        var removed = original.Roles
            .Except(definition.Roles, StringComparer.OrdinalIgnoreCase)
            .Select(role => $"ALTER SERVER ROLE {Quote(role)} DROP MEMBER {name};");

        return added.Concat(removed);
    }

    private static string Password(string password, ServerLoginDefinition definition) =>
        definition.MustChangePassword
            ? $"{SqlServerLiteral.Quote(password)} MUST_CHANGE"
            : SqlServerLiteral.Quote(password);

    private static string AddMember(string role, string quotedLogin) =>
        $"ALTER SERVER ROLE {Quote(role)} ADD MEMBER {quotedLogin};";

    private static string OnOff(bool value) => value ? "ON" : "OFF";

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
