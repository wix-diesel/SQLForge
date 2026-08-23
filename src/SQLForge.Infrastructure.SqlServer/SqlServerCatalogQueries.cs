namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// カタログ照会の SQL。データベース名だけは 3 部名として文面に埋めるので
/// <see cref="SqlServerIdentifier.Quote"/> を通す。それ以外の値はすべてパラメータで渡す。
/// </summary>
internal static class SqlServerCatalogQueries
{
    /// <summary>サーバーの素性。SERVERPROPERTY はどのエディションでも読める。</summary>
    public const string ServerInfo = """
        SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS product_version,
               CONVERT(nvarchar(128), SERVERPROPERTY('Edition'))        AS edition,
               CONVERT(int,           SERVERPROPERTY('EngineEdition'))  AS engine_edition;
        """;

    /// <summary>
    /// この接続が暗号化されているか。sys.dm_exec_connections は VIEW SERVER STATE が要るので、
    /// 読めないことも普通にある。読めなければ「不明」として扱う。
    /// </summary>
    public const string EncryptionState = """
        SELECT CONVERT(nvarchar(10), c.encrypt_option)
        FROM sys.dm_exec_connections AS c
        WHERE c.session_id = @@SPID;
        """;

    /// <summary>
    /// データベース一覧。database_id 1〜4 は master / tempdb / model / msdb。
    /// HAS_DBACCESS はアクセスできないデータベースで 0、判定できないと NULL を返す。
    /// </summary>
    public const string Databases = """
        SELECT d.name                                                              AS name,
               CONVERT(bit, CASE WHEN d.database_id <= 4 THEN 1 ELSE 0 END)        AS is_system,
               CONVERT(bit, CASE WHEN d.state_desc = 'ONLINE'
                                  AND ISNULL(HAS_DBACCESS(d.name), 0) = 1
                                 THEN 1 ELSE 0 END)                                AS is_accessible,
               d.collation_name                                                    AS collation_name
        FROM sys.databases AS d;
        """;

    /// <summary>
    /// スキーマ一覧。schema_id は dbo=1・guest=2・INFORMATION_SCHEMA=3・sys=4 で、
    /// 固定データベースロールのスキーマ（db_owner など）は 16384 以降。
    /// 名前ではなく id で判定するので、照合順序や言語設定に左右されない。
    /// </summary>
    public const string SchemasFormat = """
        SELECT s.name                                                              AS name,
               CONVERT(bit, CASE WHEN s.schema_id <> 1
                                  AND (s.schema_id < 5 OR s.schema_id > 16383)
                                 THEN 1 ELSE 0 END)                                AS is_system
        FROM {0}.sys.schemas AS s;
        """;

    /// <summary>
    /// テーブル一覧と概算行数。index_id 0 はヒープ、1 はクラスター化インデックスで、
    /// テーブルはそのどちらか一方しか持たないため、両方を足せば行数になる。
    /// is_ms_shipped = 0 でエンジンが用意したテーブルを外す。
    /// </summary>
    public const string TablesFormat = """
        SELECT t.name                                        AS name,
               CONVERT(bigint, SUM(ISNULL(p.rows, 0)))       AS row_count
        FROM {0}.sys.tables AS t
        INNER JOIN {0}.sys.schemas AS s ON s.schema_id = t.schema_id
        LEFT JOIN {0}.sys.partitions AS p
               ON p.object_id = t.object_id AND p.index_id IN (0, 1)
        WHERE s.name = @schema AND t.is_ms_shipped = 0
        GROUP BY t.name;
        """;

    /// <summary>
    /// カラム定義一覧。column_id はテーブル定義での並び順そのもの。
    /// 主キーは sys.indexes.is_primary_key を持つインデックスの列から拾う
    /// （PRIMARY KEY 制約は一意インデックスとして実体化されるため）。
    /// </summary>
    public const string ColumnsFormat = """
        SELECT c.name                                                              AS name,
               c.column_id                                                        AS ordinal_position,
               ty.name                                                            AS type_name,
               c.max_length                                                        AS max_length,
               c.precision                                                         AS precision,
               c.scale                                                             AS scale,
               c.is_nullable                                                       AS is_nullable,
               c.is_identity                                                       AS is_identity,
               CONVERT(bit, CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END)  AS is_primary_key
        FROM {0}.sys.columns AS c
        INNER JOIN {0}.sys.tables AS t ON t.object_id = c.object_id
        INNER JOIN {0}.sys.schemas AS s ON s.schema_id = t.schema_id
        INNER JOIN {0}.sys.types AS ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN (
            SELECT ic.object_id, ic.column_id
            FROM {0}.sys.index_columns AS ic
            INNER JOIN {0}.sys.indexes AS i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            WHERE i.is_primary_key = 1
        ) AS pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
        WHERE s.name = @schema AND t.name = @table
        ORDER BY c.column_id;
        """;

    /// <summary>
    /// ストアド プロシージャ一覧とパラメーター数。sys.procedures は type IN ('P', 'RF') のみを
    /// 返すビューなので、スカラー関数などは含まれない。is_ms_shipped = 0 でエンジンが
    /// 用意したものを外すのはテーブルと同じ。parameter_id = 0 は戻り値なので数に入れない。
    /// </summary>
    public const string StoredProceduresFormat = """
        SELECT p.name                                                              AS name,
               (
                   SELECT COUNT(*)
                   FROM {0}.sys.parameters AS pr
                   WHERE pr.object_id = p.object_id AND pr.parameter_id > 0
               )                                                                   AS parameter_count
        FROM {0}.sys.procedures AS p
        INNER JOIN {0}.sys.schemas AS s ON s.schema_id = p.schema_id
        WHERE s.name = @schema AND p.is_ms_shipped = 0
        ORDER BY p.name;
        """;

    /// <summary>
    /// ストアド プロシージャのパラメーター一覧。parameter_id は宣言順そのもの。
    /// parameter_id = 0 は戻り値を表す行なので除く。
    /// </summary>
    public const string StoredProcedureParametersFormat = """
        SELECT pr.name                                                            AS name,
               pr.parameter_id                                                    AS ordinal_position,
               ty.name                                                            AS type_name,
               pr.max_length                                                      AS max_length,
               pr.precision                                                       AS precision,
               pr.scale                                                           AS scale,
               pr.is_output                                                       AS is_output,
               CONVERT(bit, CASE WHEN pr.has_default_value = 1 THEN 1 ELSE 0 END) AS has_default_value
        FROM {0}.sys.parameters AS pr
        INNER JOIN {0}.sys.procedures AS p ON p.object_id = pr.object_id
        INNER JOIN {0}.sys.schemas AS s ON s.schema_id = p.schema_id
        INNER JOIN {0}.sys.types AS ty ON ty.user_type_id = pr.user_type_id
        WHERE s.name = @schema AND p.name = @procedure AND pr.parameter_id > 0
        ORDER BY pr.parameter_id;
        """;
}
