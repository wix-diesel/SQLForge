namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// ロール照会の SQL。データベース名だけは 3 部名として文面に埋めるので
/// <see cref="SqlServerIdentifier.Quote"/> を通す。
/// </summary>
internal static class SqlServerRoleQueries
{
    /// <summary>
    /// データベース ロールの一覧と、所属・所有スキーマ。3 つの結果セットを 1 度に返す。
    ///
    /// principal_id 0 は public で、すべてのユーザーが暗黙に持つので外す。
    /// ## で始まる名前はエンジンが用意したもの。アプリケーション ロール（type = 'A'）は
    /// メンバーを取れないので出さない。
    /// </summary>
    public const string RolesFormat = """
        SELECT r.name                                                              AS name,
               o.name                                                              AS owner_name,
               CONVERT(bit, r.is_fixed_role)                                       AS is_fixed_role
        FROM {0}.sys.database_principals AS r
        LEFT JOIN {0}.sys.database_principals AS o ON o.principal_id = r.owning_principal_id
        WHERE r.type = 'R' AND r.principal_id <> 0 AND r.name NOT LIKE N'##%';

        SELECT r.name                                                              AS role_name,
               m.name                                                              AS member_name
        FROM {0}.sys.database_role_members AS rm
        INNER JOIN {0}.sys.database_principals AS r ON r.principal_id = rm.role_principal_id
        INNER JOIN {0}.sys.database_principals AS m ON m.principal_id = rm.member_principal_id
        WHERE r.principal_id <> 0;

        SELECT p.name                                                              AS owner_name,
               s.name                                                              AS schema_name
        FROM {0}.sys.schemas AS s
        INNER JOIN {0}.sys.database_principals AS p ON p.principal_id = s.principal_id
        WHERE p.type = 'R';
        """;

    /// <summary>
    /// サーバー ロールの一覧と、所属。2 つの結果セットを 1 度に返す。
    ///
    /// public はすべてのログインが暗黙に持つので外す。所属の結果セットは
    /// 「誰がどのロールに入っているか」で、メンバーが自身もロールなら
    /// そのロールにとってはメンバーシップになる。
    /// </summary>
    public const string ServerRoles = """
        SELECT r.name                                                              AS name,
               o.name                                                              AS owner_name,
               CONVERT(bit, r.is_fixed_role)                                       AS is_fixed_role
        FROM sys.server_principals AS r
        LEFT JOIN sys.server_principals AS o ON o.principal_id = r.owning_principal_id
        WHERE r.type = 'R' AND r.name <> N'public';

        SELECT r.name                                                              AS role_name,
               m.name                                                              AS member_name,
               CONVERT(char(1), m.type)                                            AS member_type
        FROM sys.server_role_members AS rm
        INNER JOIN sys.server_principals AS r ON r.principal_id = rm.role_principal_id
        INNER JOIN sys.server_principals AS m ON m.principal_id = rm.member_principal_id;
        """;
}
