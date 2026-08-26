using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>データベース 1 つぶんの文面。マッピングはデータベースごとに別の場所で流す。</summary>
/// <param name="Database">流す先のデータベース。</param>
/// <param name="Statements">その中で順に流す文面。</param>
internal sealed record DatabaseStatements(DatabaseName Database, IReadOnlyList<string> Statements);

/// <summary>
/// ユーザー マッピングの文面。対応づけの実体は「そのデータベースの中の、ログインに紐づくユーザー」
/// なので、文面はユーザーの追加・編集・削除そのものになる。組み立てはユーザー側の
/// <see cref="SqlServerSecurityStatements"/> をそのまま使う。
/// </summary>
internal static class SqlServerMappingStatements
{
    public static IReadOnlyList<DatabaseStatements> Plan(
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(desired);

        var plan = new List<DatabaseStatements>();

        foreach (var mapping in desired)
        {
            var before = Find(original, mapping.Database);

            var statements = before is null
                ? SqlServerSecurityStatements.Create(ToDefinition(login, mapping))
                : SqlServerSecurityStatements.Alter(ToDescriptor(login, before), ToDefinition(login, mapping));

            if (statements.Count > 0)
            {
                plan.Add(new DatabaseStatements(mapping.Database, statements));
            }
        }

        // 望みの姿に無いデータベースからは、ユーザーごと外す。
        plan.AddRange(original
            .Where(mapping => Find(desired, mapping.Database) is null)
            .Select(mapping => new DatabaseStatements(
                mapping.Database,
                [SqlServerSecurityStatements.Drop(mapping.User)])));

        return plan;
    }

    private static LoginUserMapping? Find(IReadOnlyList<LoginUserMapping> mappings, DatabaseName database) =>
        mappings.FirstOrDefault(mapping =>
            string.Equals(mapping.Database.Value, database.Value, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 種類は必ず「ログインありの SQL ユーザー」として扱う。Windows ログインの相手でも
    /// 文面は CREATE USER ... FOR LOGIN で変わらず、ユーザーの種類はログインの側の
    /// 素性から決まるため。
    /// </summary>
    private static DatabaseUserDefinition ToDefinition(ServerLoginName login, LoginUserMapping mapping) =>
        new(mapping.User, DatabaseUserType.SqlUserWithLogin, login.Value, mapping.DefaultSchema)
        {
            Roles = mapping.Roles
        };

    private static DatabaseUserDescriptor ToDescriptor(ServerLoginName login, LoginUserMapping mapping) =>
        new(mapping.User, DatabaseUserType.SqlUserWithLogin, login.Value, mapping.DefaultSchema)
        {
            Roles = mapping.Roles
        };
}
