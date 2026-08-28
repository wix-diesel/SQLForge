namespace SQLForge.Infrastructure.PostgreSql;

/// <summary>
/// カタログ照会の SQL。値はすべてパラメータで渡す。
///
/// SQL Server と違ってデータベースをまたいで読めないので、文面に 3 部名は出てこない。
/// 代わりに、読む前に接続をそのデータベースへ張り直すのが
/// <see cref="PostgreSqlSession"/> の受け持ちになる。
/// </summary>
internal static class PostgreSqlCatalogQueries
{
    /// <summary>
    /// サーバーの素性。server_version は "16.2" のような表示用の版で、
    /// version() は配布物やビルド環境まで含む 1 行。
    /// </summary>
    public const string ServerInfo = """
        SELECT current_setting('server_version') AS server_version,
               version()                         AS banner;
        """;

    /// <summary>
    /// この接続が暗号化されているか。pg_stat_ssl の自分の行はどの利用者でも読める
    /// （他の利用者の行は伏せられるが、ここで要るのは自分の行だけ）。
    /// </summary>
    public const string EncryptionState = """
        SELECT s.ssl
        FROM pg_catalog.pg_stat_ssl AS s
        WHERE s.pid = pg_backend_pid();
        """;

    /// <summary>
    /// データベース一覧。pg_database は共有カタログなので、どのデータベースに繋いでいても読める。
    ///
    /// テンプレート（template0 / template1）と postgres はエンジンが用意したもの。
    /// 接続を受け付けない設定（datallowconn = false）と、CONNECT 権限の無いものは
    /// 一覧に出しても展開はさせない。作成日時に当たる列は PostgreSQL に無い。
    /// </summary>
    public const string Databases = """
        SELECT d.datname                                     AS name,
               (d.datname IN ('postgres', 'template0', 'template1'))
                                                             AS is_system,
               (d.datallowconn
                AND pg_catalog.has_database_privilege(d.oid, 'CONNECT'))
                                                             AS is_accessible,
               NULLIF(d.datcollate, '')                      AS collation_name
        FROM pg_catalog.pg_database AS d;
        """;

    /// <summary>
    /// スキーマ一覧と、その所有者。information_schema と pg_ で始まるもの
    /// （pg_catalog・pg_toast・セッションごとの pg_temp_*）がエンジンの用意したもの。
    ///
    /// 名前の頭 3 文字で見るのは、LIKE 'pg_%' だと下線がワイルドカードとして働き、
    /// pgcrypto のような名前まで拾ってしまうため。
    /// </summary>
    public const string Schemas = """
        SELECT n.nspname                                     AS name,
               (n.nspname = 'information_schema'
                OR left(n.nspname, 3) = 'pg_')               AS is_system,
               pg_catalog.pg_get_userbyid(n.nspowner)        AS owner_name
        FROM pg_catalog.pg_namespace AS n;
        """;

    /// <summary>
    /// テーブル一覧と概算行数。reltuples は ANALYZE が書く見積もりで、
    /// 一度も走っていないテーブルでは -1（PostgreSQL 14 以降）。その場合は「不明」にする。
    ///
    /// relkind は 'r'（普通のテーブル）と 'p'（パーティションの親）だけを採る。
    /// 作成日時に当たる列は PostgreSQL に無い。
    /// </summary>
    public const string Tables = """
        SELECT c.relname                                     AS name,
               CASE WHEN c.reltuples < 0 THEN NULL
                    ELSE c.reltuples::bigint END             AS row_count
        FROM pg_catalog.pg_class AS c
        INNER JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
        WHERE n.nspname = @schema AND c.relkind IN ('r', 'p');
        """;

    /// <summary>
    /// カラム定義一覧。attnum がテーブル定義での並び順で、0 以下はシステム列、
    /// attisdropped は消した列の跡なので、どちらも外す。
    ///
    /// 「採番される列」は IDENTITY（attidentity）と、serial 型が付ける
    /// nextval() の既定値の両方を指す。主キーは pg_index の主キーインデックスから拾う。
    /// </summary>
    public const string Columns = """
        SELECT a.attname                                             AS name,
               a.attnum::int                                         AS ordinal_position,
               pg_catalog.format_type(a.atttypid, a.atttypmod)       AS data_type,
               (NOT a.attnotnull)                                    AS is_nullable,
               (a.attidentity IN ('a', 'd')
                OR COALESCE(pg_catalog.pg_get_expr(ad.adbin, ad.adrelid), '')
                   LIKE 'nextval(%')                                 AS is_identity,
               (i.indrelid IS NOT NULL)                              AS is_primary_key
        FROM pg_catalog.pg_attribute AS a
        INNER JOIN pg_catalog.pg_class AS c ON c.oid = a.attrelid
        INNER JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
        LEFT JOIN pg_catalog.pg_attrdef AS ad
               ON ad.adrelid = a.attrelid AND ad.adnum = a.attnum
        LEFT JOIN pg_catalog.pg_index AS i
               ON i.indrelid = c.oid AND i.indisprimary AND a.attnum = ANY (i.indkey)
        WHERE n.nspname = @schema AND c.relname = @table
          AND a.attnum > 0 AND NOT a.attisdropped;
        """;

    /// <summary>
    /// ストアド プロシージャ一覧。PostgreSQL は関数（prokind = 'f'）と
    /// プロシージャ（'p'）を分けているが、ツリーでは 1 つの見出しにまとめて出す。
    /// 集約関数・ウィンドウ関数は呼び出すものではないので外す。
    ///
    /// 同じ名前で引数違いのものは別々の行になる（PostgreSQL は多重定義できる）。
    /// 作成日時に当たる列は PostgreSQL に無い。
    /// </summary>
    public const string StoredProcedures = """
        SELECT p.proname                                     AS name,
               p.pronargs::int                               AS parameter_count
        FROM pg_catalog.pg_proc AS p
        INNER JOIN pg_catalog.pg_namespace AS n ON n.oid = p.pronamespace
        WHERE n.nspname = @schema AND p.prokind IN ('f', 'p');
        """;

    /// <summary>
    /// ストアド プロシージャのパラメーター。型と既定値の有無まで揃っているので
    /// information_schema から読む。
    ///
    /// 多重定義があるときは 1 つに絞る（呼び出し側は名前しか持たないため）。
    /// 名前の無い引数は $1 のように位置で呼ぶ。
    /// </summary>
    public const string StoredProcedureParameters = """
        SELECT COALESCE(a.parameter_name::text, '$' || a.ordinal_position::text)
                                                             AS name,
               a.ordinal_position::int                       AS ordinal_position,
               a.data_type                                   AS data_type,
               (a.parameter_mode IN ('OUT', 'INOUT'))        AS is_output,
               (a.parameter_default IS NOT NULL)             AS has_default
        FROM information_schema.parameters AS a
        WHERE a.specific_schema = @schema
          AND a.specific_name = (
              SELECT r.specific_name
              FROM information_schema.routines AS r
              WHERE r.specific_schema = @schema AND r.routine_name = @procedure
              ORDER BY r.specific_name
              LIMIT 1);
        """;
}
