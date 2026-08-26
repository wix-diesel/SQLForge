namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 権限とリソース候補の照会 SQL。データベース名だけは 3 部名として文面に埋めるので
/// <see cref="SqlServerIdentifier.Quote"/> を通し、主体の名前はパラメータで渡す。
/// </summary>
internal static class SqlServerPermissionQueries
{
    /// <summary>
    /// データベース スコープの主体に明示的に付いている権限。
    ///
    /// class 0 はデータベースそのもの、1 はオブジェクト（テーブル・プロシージャ）、3 はスキーマ。
    /// minor_id が 0 でない行は列単位の権限で、この版では扱わないので外す。
    /// </summary>
    public const string DatabasePermissionsFormat = """
        SELECT CONVERT(int, p.class)                                               AS class_id,
               p.permission_name                                                   AS permission_name,
               p.state_desc                                                        AS state_desc,
               s.name                                                              AS schema_name,
               o.name                                                              AS object_name,
               os.name                                                             AS object_schema,
               CONVERT(char(2), o.type)                                            AS object_type
        FROM {0}.sys.database_permissions AS p
        INNER JOIN {0}.sys.database_principals AS g ON g.principal_id = p.grantee_principal_id
        LEFT JOIN {0}.sys.schemas AS s ON p.class = 3 AND s.schema_id = p.major_id
        LEFT JOIN {0}.sys.objects AS o ON p.class = 1 AND o.object_id = p.major_id
        LEFT JOIN {0}.sys.schemas AS os ON os.schema_id = o.schema_id
        WHERE g.name = @principal AND p.minor_id = 0 AND p.class IN (0, 1, 3);
        """;

    /// <summary>
    /// サーバー スコープの主体に明示的に付いている権限。
    /// class 100 はサーバーそのもの、101 はログイン（サーバー プリンシパル）。
    /// </summary>
    public const string ServerPermissions = """
        SELECT CONVERT(int, p.class)                                               AS class_id,
               p.permission_name                                                   AS permission_name,
               p.state_desc                                                        AS state_desc,
               l.name                                                              AS login_name
        FROM sys.server_permissions AS p
        INNER JOIN sys.server_principals AS g ON g.principal_id = p.grantee_principal_id
        LEFT JOIN sys.server_principals AS l ON p.class = 101 AND l.principal_id = p.major_id
        WHERE g.name = @principal AND p.class IN (100, 101);
        """;

    /// <summary>サーバーそのものの名前。リソースの一覧に 1 件だけ出す。</summary>
    public const string ServerName = """
        SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS name;
        """;

    /// <summary>ログインの候補。エンジンが用意したもの（## で始まる名前）は出さない。</summary>
    public const string Logins = """
        SELECT p.name AS name
        FROM sys.server_principals AS p
        WHERE p.type IN ('S', 'U', 'G', 'C', 'K', 'E', 'X') AND p.name NOT LIKE N'##%';
        """;

    /// <summary>データベースの候補。</summary>
    public const string Databases = """
        SELECT d.name AS name
        FROM sys.databases AS d;
        """;

    /// <summary>スキーマの候補。</summary>
    public const string SchemasFormat = """
        SELECT s.name AS name
        FROM {0}.sys.schemas AS s;
        """;

    /// <summary>テーブルの候補。エンジンが用意したテーブルは外す。</summary>
    public const string TablesFormat = """
        SELECT s.name AS schema_name,
               t.name AS name
        FROM {0}.sys.tables AS t
        INNER JOIN {0}.sys.schemas AS s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0;
        """;

    /// <summary>ストアド プロシージャの候補。エンジンが用意したものは外す。</summary>
    public const string StoredProceduresFormat = """
        SELECT s.name AS schema_name,
               p.name AS name
        FROM {0}.sys.procedures AS p
        INNER JOIN {0}.sys.schemas AS s ON s.schema_id = p.schema_id
        WHERE p.is_ms_shipped = 0;
        """;
}
