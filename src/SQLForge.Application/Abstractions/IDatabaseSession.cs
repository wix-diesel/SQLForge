using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Editing;
using SQLForge.Domain.Query;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Abstractions;

/// <summary>
/// 開いている接続 1 本。オブジェクトエクスプローラーはこの口だけを通してカタログを読む。
///
/// エンジン差はすべて実装側に閉じ込める。たとえば SQL Server は 3 部名 (db.sys.tables) で
/// 他のデータベースを読めるが、PostgreSQL はデータベースをまたげないので接続を張り直す。
/// どちらでもこの口の形は変わらない。
/// </summary>
public interface IDatabaseSession : IAsyncDisposable
{
    /// <summary>この接続を開くのに使った接続情報。</summary>
    ConnectionProfile Profile { get; }

    /// <summary>接続時に読み取ったサーバーの素性。</summary>
    ServerInfo Server { get; }

    /// <summary>サーバー上のデータベース一覧。</summary>
    Task<IReadOnlyList<DatabaseDescriptor>> ListDatabasesAsync(CancellationToken cancellationToken = default);

    /// <summary>指定データベース内のスキーマ一覧。</summary>
    Task<IReadOnlyList<SchemaDescriptor>> ListSchemasAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default);

    /// <summary>指定スキーマ内のテーブル一覧。</summary>
    Task<IReadOnlyList<TableDescriptor>> ListTablesAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default);

    /// <summary>指定テーブルのカラム定義一覧。</summary>
    Task<IReadOnlyList<ColumnDescriptor>> ListColumnsAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken = default);

    /// <summary>指定スキーマ内のストアド プロシージャ一覧。</summary>
    Task<IReadOnlyList<StoredProcedureDescriptor>> ListStoredProceduresAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default);

    /// <summary>指定ストアド プロシージャのパラメーター一覧。</summary>
    Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ListStoredProcedureParametersAsync(
        DatabaseName database,
        SchemaName schema,
        string procedure,
        CancellationToken cancellationToken = default);

    /// <summary>サーバー上のログイン一覧。SSMS の [セキュリティ] → [ログイン] にあたる。</summary>
    Task<IReadOnlyList<ServerLoginDescriptor>> ListServerLoginsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>サーバー ロールの一覧（public を除く）。</summary>
    Task<IReadOnlyList<ServerRoleDescriptor>> ListServerRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>サーバー ロールを 1 件作る。所有者・メンバー・メンバーシップまで含めて 1 つの操作として扱う。</summary>
    Task CreateServerRoleAsync(
        ServerRoleDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// サーバー ロールを 1 件作り替える。<paramref name="original"/> は変更前の姿で、
    /// 実際に変わったところだけを文面に出すために使う。
    /// </summary>
    Task AlterServerRoleAsync(
        ServerRoleDescriptor original,
        ServerRoleDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>サーバー ロールを 1 件削除する。</summary>
    Task DropServerRoleAsync(
        RoleName role,
        CancellationToken cancellationToken = default);

    /// <summary>ログインを 1 件作る。ロールへの追加や無効化まで含めて 1 つの操作として扱う。</summary>
    Task CreateServerLoginAsync(
        ServerLoginDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ログインを 1 件作り替える。<paramref name="original"/> は変更前の姿で、
    /// 実際に変わったところだけを文面に出すために使う。
    /// </summary>
    Task AlterServerLoginAsync(
        ServerLoginDescriptor original,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>ログインを 1 件削除する。</summary>
    Task DropServerLoginAsync(
        ServerLoginName login,
        CancellationToken cancellationToken = default);

    /// <summary>指定データベース内のユーザー一覧。</summary>
    Task<IReadOnlyList<DatabaseUserDescriptor>> ListDatabaseUsersAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default);

    /// <summary>指定データベース内のデータベース ロールの一覧（public を除く）。</summary>
    Task<IReadOnlyList<DatabaseRoleDescriptor>> ListDatabaseRolesAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// データベース ロールを 1 件作る。所有者・メンバー・所有スキーマまで含めて、
    /// 途中で失敗したら何も残さない。
    /// </summary>
    Task CreateDatabaseRoleAsync(
        DatabaseName database,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// データベース ロールを 1 件作り替える。<paramref name="original"/> は変更前の姿で、
    /// 実際に変わったところだけを文面に出すために使う。
    /// </summary>
    Task AlterDatabaseRoleAsync(
        DatabaseName database,
        DatabaseRoleDescriptor original,
        DatabaseRoleDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>データベース ロールを 1 件削除する。</summary>
    Task DropDatabaseRoleAsync(
        DatabaseName database,
        RoleName role,
        CancellationToken cancellationToken = default);

    /// <summary>ユーザーを 1 件作る。ロールへの追加まで含めて、途中で失敗したら何も残さない。</summary>
    Task CreateDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーを 1 件作り替える。<paramref name="original"/> は変更前の姿で、
    /// 実際に変わったところだけを文面に出すために使う。
    /// </summary>
    Task AlterDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserDescriptor original,
        DatabaseUserDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>ユーザーを 1 件削除する。</summary>
    Task DropDatabaseUserAsync(
        DatabaseName database,
        DatabaseUserName user,
        CancellationToken cancellationToken = default);

    /// <summary>スキーマを 1 件作る。所有者まで含めて 1 つの操作として扱う。</summary>
    Task CreateSchemaAsync(
        DatabaseName database,
        SchemaDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// スキーマの所有者を付け替える。名前は変えられない（エンジンにその文面が無い）ので、
    /// <paramref name="original"/> と <paramref name="definition"/> の名前は必ず同じになる。
    /// </summary>
    Task AlterSchemaAsync(
        DatabaseName database,
        SchemaDescriptor original,
        SchemaDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>スキーマを 1 件削除する。</summary>
    Task DropSchemaAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ログイン 1 件のユーザー マッピング。どのデータベースに、どの名前のユーザーとして
    /// 居るのかを、データベースを横断して読む。
    /// </summary>
    Task<IReadOnlyList<LoginUserMapping>> ListLoginUserMappingsAsync(
        ServerLoginName login,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザー マッピングを望みの姿へ揃える。<paramref name="original"/> にしか無い
    /// データベースからはユーザーを外し、<paramref name="desired"/> にしか無いところへは作る。
    ///
    /// データベースごとに別の文面になるので、どこかで失敗しても他のデータベースの
    /// 結果は残る。呼び出し側は読み直して実際の姿を出し直すこと。
    /// </summary>
    Task ApplyLoginUserMappingsAsync(
        ServerLoginName login,
        IReadOnlyList<LoginUserMapping> original,
        IReadOnlyList<LoginUserMapping> desired,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 主体 1 人に明示的に付いている権限。<paramref name="database"/> は
    /// データベース スコープの主体（ユーザー・データベース ロール）の居場所で、
    /// サーバー スコープの主体では null。
    /// </summary>
    Task<IReadOnlyList<PermissionEntry>> ListPermissionsAsync(
        SecurityPrincipal principal,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 権限を望みの姿へ揃える。<paramref name="desired"/> に出てくる「相手 × 権限」だけを
    /// 見て、状態が変わったものに GRANT / DENY / REVOKE を出す。
    /// <paramref name="desired"/> に出てこないものは触らない（この版が知らない権限を
    /// 黙って落とさないため）。
    /// </summary>
    Task ApplyPermissionsAsync(
        SecurityPrincipal principal,
        DatabaseName? database,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 権限を付けられるリソースの候補。<paramref name="database"/> は
    /// データベース スコープの種類（スキーマ・テーブルなど）を読むときの居場所。
    /// </summary>
    Task<IReadOnlyList<SecurableReference>> ListSecurablesAsync(
        SecurableKind kind,
        DatabaseName? database = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// エディタの文面をこの接続で実行する。文面はそのまま送る（分割も書き換えもしない）。
    ///
    /// <paramref name="database"/> は実行先。エディタの文面は「今いるデータベース」を
    /// 前提に書かれるので、実行の直前にそこへ合わせるのは実装側の責務になる。
    /// </summary>
    /// <param name="maxRows">結果セットごとに読む行数の上限。超えた分は読まずに捨てる。</param>
    Task<QueryResult> ExecuteQueryAsync(
        DatabaseName database,
        string sql,
        int maxRows,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定テーブルの先頭 <paramref name="maxRows"/> 行を編集用に読む。
    ///
    /// 列の素性（鍵にできるか、書き換えられるか）はエンジンごとに違うので、
    /// その判断まで含めて実装側が済ませる。並び順は指定しない。
    /// </summary>
    Task<EditableRowSet> ReadEditableRowsAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        int maxRows,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 編集グリッドのセル 1 つを書き戻す。ちょうど 1 行に当たらない更新は成立させず、
    /// 何も残さないのは実装側の責務（条件に複数行が当たる更新は取り消す）。
    /// </summary>
    /// <returns>実際に更新した行数。</returns>
    Task<int> UpdateTableCellAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        TableCellUpdate update,
        CancellationToken cancellationToken = default);
}
