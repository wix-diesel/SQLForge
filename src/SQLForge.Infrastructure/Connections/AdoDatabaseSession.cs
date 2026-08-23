using System.Data.Common;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// ADO.NET を使うセッションで共通の部分。接続の寿命と、
/// 同時に投げられたカタログ照会の直列化だけを引き受ける。
///
/// エンジンごとに違うのはカタログの読み方だけなので、派生クラスは Read*Async を
/// 3 つ埋めればよい。ツリーは複数のノードを同時に展開できるが、
/// <see cref="DbConnection"/> は 1 本で複数の照会を同時に走らせられないため、
/// ここで門を 1 つに絞っている。
/// </summary>
public abstract class AdoDatabaseSession : IDatabaseSession
{
    private readonly DbConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    protected AdoDatabaseSession(ConnectionProfile profile, DbConnection connection, ServerInfo server)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Server = server ?? throw new ArgumentNullException(nameof(server));
    }

    public ConnectionProfile Profile { get; }

    public ServerInfo Server { get; }

    public Task<IReadOnlyList<DatabaseDescriptor>> ListDatabasesAsync(CancellationToken cancellationToken = default) =>
        QueryAsync(ReadDatabasesAsync, cancellationToken);

    public Task<IReadOnlyList<SchemaDescriptor>> ListSchemasAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default) =>
        QueryAsync((connection, token) => ReadSchemasAsync(connection, database, token), cancellationToken);

    public Task<IReadOnlyList<TableDescriptor>> ListTablesAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default) =>
        QueryAsync((connection, token) => ReadTablesAsync(connection, database, schema, token), cancellationToken);

    protected abstract Task<IReadOnlyList<DatabaseDescriptor>> ReadDatabasesAsync(
        DbConnection connection,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<SchemaDescriptor>> ReadSchemasAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<TableDescriptor>> ReadTablesAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken);

    private async Task<T> QueryAsync<T>(
        Func<DbConnection, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await read(_connection, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 実行中の照会が門を返すのを待ってから接続を閉じる。待たずに閉じると、
    /// 読み取り中の <see cref="DbDataReader"/> の足元で接続が消える。
    ///
    /// 門 (<see cref="SemaphoreSlim"/>) 自体は破棄しない。破棄すると、実行中の照会が
    /// finally で Release() したときに <see cref="ObjectDisposedException"/> になる。
    /// SemaphoreSlim の破棄が要るのは AvailableWaitHandle を触った場合だけで、ここでは触っていない。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // 先に閉じたことにして、以降の照会は門を待たずに弾く。
        _disposed = true;

        // 実行中の照会はコマンドのタイムアウトで必ず終わるので、ここで止まり続けることはない。
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        GC.SuppressFinalize(this);
    }
}
