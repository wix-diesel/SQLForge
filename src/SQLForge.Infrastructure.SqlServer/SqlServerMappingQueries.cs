namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// ユーザー マッピング照会の SQL。データベース名だけは 3 部名として文面に埋めるので
/// <see cref="SqlServerIdentifier.Quote"/> を通す。ログインは SID で辿るのでパラメータで渡す。
/// </summary>
internal static class SqlServerMappingQueries
{
    /// <summary>
    /// ログインの SID。データベースの中のユーザーは名前ではなく SID でログインと結び付くので、
    /// 対応づけを辿るにはまずこれが要る。
    /// </summary>
    public const string LoginSid = """
        SELECT p.sid AS sid
        FROM sys.server_principals AS p
        WHERE p.name = @login;
        """;

    /// <summary>
    /// あるデータベースの中で、その SID を持つユーザーと所属ロール。2 つの結果セットを返す。
    /// ロールとアプリケーション ロールは SID を持たないので、type で外すまでもなく当たらない。
    /// </summary>
    public const string MappingFormat = """
        SELECT p.name                                                              AS user_name,
               p.default_schema_name                                               AS default_schema_name
        FROM {0}.sys.database_principals AS p
        WHERE p.sid = @sid;

        SELECT r.name                                                              AS role_name
        FROM {0}.sys.database_role_members AS rm
        INNER JOIN {0}.sys.database_principals AS r ON r.principal_id = rm.role_principal_id
        INNER JOIN {0}.sys.database_principals AS m ON m.principal_id = rm.member_principal_id
        WHERE m.sid = @sid AND r.principal_id <> 0;
        """;
}
