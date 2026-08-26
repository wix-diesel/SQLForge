namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// ログイン照会の SQL。データベース ユーザーと違ってスコープがサーバーなので、
/// 3 部名も埋め込むものも無く、文面は固定でよい。
/// </summary>
internal static class SqlServerLoginQueries
{
    /// <summary>
    /// ログイン一覧と、そのサーバー ロールの所属。2 つの結果セットを 1 度に返す。
    ///
    /// principal_id が 100 以下のものは sa とエンジンが用意した principal で、
    /// ## で始まる名前はエンジンが用意した証明書ログイン。どちらも編集させない。
    /// パスワードの規則は SQL Server 認証のログインだけが持つので、sys.sql_logins は
    /// LEFT JOIN にして、ほかの種類では NULL のままにする。
    /// </summary>
    public const string Logins = """
        SELECT p.name                                                              AS name,
               CONVERT(char(1), p.type)                                            AS type_code,
               p.default_database_name                                             AS default_database_name,
               CONVERT(bit, p.is_disabled)                                         AS is_disabled,
               CONVERT(bit, CASE WHEN p.principal_id <= 100 OR p.name LIKE N'##%'
                                 THEN 1 ELSE 0 END)                                AS is_system,
               s.is_policy_checked                                                 AS is_policy_checked,
               s.is_expiration_checked                                             AS is_expiration_checked
        FROM sys.server_principals AS p
        LEFT JOIN sys.sql_logins AS s ON s.principal_id = p.principal_id
        WHERE p.type IN ('S', 'U', 'G', 'C', 'K', 'E', 'X');

        SELECT m.name                                                              AS member_name,
               r.name                                                              AS role_name
        FROM sys.server_role_members AS rm
        INNER JOIN sys.server_principals AS r ON r.principal_id = rm.role_principal_id
        INNER JOIN sys.server_principals AS m ON m.principal_id = rm.member_principal_id
        WHERE m.type <> 'R';
        """;

    /// <summary>
    /// サーバー ロール一覧。public はすべてのログインが暗黙に持つので外す。
    /// ## で始まる名前はエンジンが用意したもの。
    /// </summary>
    public const string Roles = """
        SELECT r.name AS name
        FROM sys.server_principals AS r
        WHERE r.type = 'R' AND r.name <> N'public' AND r.name NOT LIKE N'##%';
        """;
}
