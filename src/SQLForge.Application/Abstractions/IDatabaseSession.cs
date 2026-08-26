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

    /// <summary>サーバー ロール名の一覧（public を除く）。</summary>
    Task<IReadOnlyList<string>> ListServerRolesAsync(CancellationToken cancellationToken = default);

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

    /// <summary>指定データベース内のデータベース ロール名の一覧（public を除く）。</summary>
    Task<IReadOnlyList<string>> ListDatabaseRolesAsync(
        DatabaseName database,
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
