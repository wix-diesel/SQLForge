using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;

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
    /// テーブルの中身をのぞく既定の文面（SSMS の「上位 N 行の選択」にあたる）。
    /// 引用符の付け方も行の絞り方もエンジンで違うので、組み立てはドライバーに任せる。
    /// </summary>
    string BuildTableQuery(DatabaseName database, SchemaName schema, string table, int maxRows);
}
