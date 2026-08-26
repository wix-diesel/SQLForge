namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// セキュリティ照会の SQL。カタログ照会と同じく、データベース名だけは 3 部名として
/// 文面に埋めるので <see cref="SqlServerIdentifier.Quote"/> を通す。
/// </summary>
internal static class SqlServerSecurityQueries
{
    /// <summary>
    /// ユーザー一覧と、そのロールの所属。2 つの結果セットを 1 度に返す。
    ///
    /// principal_id 1〜4 は dbo / guest / INFORMATION_SCHEMA / sys で、
    /// ## で始まる名前はエンジンが用意した証明書ユーザー。どちらも編集させない。
    /// 種類は type だけでは SQL ユーザーのログインの有無を分けられないので、
    /// authentication_type（0 = NONE、つまり WITHOUT LOGIN）と併せて見る。
    ///
    /// sys.server_principals はサーバー スコープのビューで、権限が無ければ行が見えない。
    /// ログイン名が読めないことは普通にあるので LEFT JOIN にして NULL を許す。
    /// </summary>
    public const string UsersFormat = """
        SELECT p.name                                                              AS name,
               CONVERT(char(1), p.type)                                            AS type_code,
               CONVERT(int, p.authentication_type)                                  AS authentication_type,
               p.default_schema_name                                               AS default_schema_name,
               l.name                                                              AS login_name,
               CONVERT(bit, CASE WHEN p.principal_id <= 4 OR p.name LIKE N'##%'
                                 THEN 1 ELSE 0 END)                                AS is_system
        FROM {0}.sys.database_principals AS p
        LEFT JOIN sys.server_principals AS l ON l.sid = p.sid
        WHERE p.type IN ('S', 'U', 'G', 'C', 'K', 'E', 'X');

        SELECT m.name                                                              AS member_name,
               r.name                                                              AS role_name
        FROM {0}.sys.database_role_members AS rm
        INNER JOIN {0}.sys.database_principals AS r ON r.principal_id = rm.role_principal_id
        INNER JOIN {0}.sys.database_principals AS m ON m.principal_id = rm.member_principal_id
        WHERE m.type NOT IN ('R', 'A') AND r.principal_id <> 0;
        """;
}
