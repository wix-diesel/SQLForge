using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;

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
}
