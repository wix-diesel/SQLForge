using System.Data.Common;
using System.Diagnostics;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// ADO.NET を使うセッションで共通の部分。接続の寿命と、
/// 同時に投げられたカタログ照会の直列化だけを引き受ける。
///
/// エンジンごとに違うのはカタログの読み方と、実行先データベースの切り替え方だけ。
/// 結果の読み取りは ADO.NET の作法だけで書けるので、どのドライバーでもここのものを使う。
/// ツリーは複数のノードを同時に展開できるが、
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

    public Task<IReadOnlyList<ColumnDescriptor>> ListColumnsAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken = default) =>
        QueryAsync((connection, token) => ReadColumnsAsync(connection, database, schema, table, token), cancellationToken);

    public Task<IReadOnlyList<StoredProcedureDescriptor>> ListStoredProceduresAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default) =>
        QueryAsync(
            (connection, token) => ReadStoredProceduresAsync(connection, database, schema, token),
            cancellationToken);

    public Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ListStoredProcedureParametersAsync(
        DatabaseName database,
        SchemaName schema,
        string procedure,
        CancellationToken cancellationToken = default) =>
        QueryAsync(
            (connection, token) => ReadStoredProcedureParametersAsync(connection, database, schema, procedure, token),
            cancellationToken);

    /// <summary>
    /// エディタの文面を実行する。カタログの照会と同じ門を通すので、
    /// ツリーの展開と同時に走っても接続を取り合うことはない。
    /// </summary>
    public Task<QueryResult> ExecuteQueryAsync(
        DatabaseName database,
        string sql,
        int maxRows,
        CancellationToken cancellationToken = default) =>
        QueryAsync(
            async (connection, token) =>
            {
                await SwitchDatabaseAsync(connection, database, token).ConfigureAwait(false);
                return await ReadResultAsync(connection, sql, maxRows, token).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// 実行先のデータベースへ切り替える。SQL Server のように 1 本の接続で切り替えられる
    /// エンジンもあれば、PostgreSQL のように接続を張り直すしかないエンジンもある。
    /// </summary>
    protected abstract Task SwitchDatabaseAsync(
        DbConnection connection,
        DatabaseName database,
        CancellationToken cancellationToken);

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

    protected abstract Task<IReadOnlyList<ColumnDescriptor>> ReadColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<StoredProcedureDescriptor>> ReadStoredProceduresAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ReadStoredProcedureParametersAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string procedure,
        CancellationToken cancellationToken);

    /// <summary>
    /// 文面を投げて結果を読む。ここは ADO.NET の作法だけで書けるので、
    /// どのドライバーでも同じものを使う。
    /// </summary>
    private static async Task<QueryResult> ReadResultAsync(
        DbConnection connection,
        string sql,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var sets = new List<QueryResultSet>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        do
        {
            // 行を返さない文（UPDATE や DDL）は列を持たない。結果セットとしては数えず、次へ進む。
            if (reader.FieldCount > 0)
            {
                sets.Add(await ReadSetAsync(reader, maxRows, cancellationToken).ConfigureAwait(false));
            }
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

        // 影響行数が確定するのはリーダーを閉じたあと。開いたまま読むと 0 や -1 が返る。
        await reader.CloseAsync().ConfigureAwait(false);

        return new QueryResult(sets, reader.RecordsAffected, Stopwatch.GetElapsedTime(started));
    }

    private static async Task<QueryResultSet> ReadSetAsync(
        DbDataReader reader,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var columns = new List<QueryColumn>(reader.FieldCount);

        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            var name = reader.GetName(ordinal);

            columns.Add(new QueryColumn(
                // 名前のない式（SELECT count(*)）にも見出しは要る。位置で呼ぶ。
                string.IsNullOrEmpty(name) ? $"(列 {ordinal + 1})" : name,
                reader.GetDataTypeName(ordinal),
                AdoValueText.IsNumeric(reader.GetFieldType(ordinal))));
        }

        var rows = new List<IReadOnlyList<string?>>();
        var isTruncated = false;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // 上限まで読んだら、残りは読まずに置いていく（NextResult が読み飛ばす）。
            if (rows.Count >= maxRows)
            {
                isTruncated = true;
                break;
            }

            var values = new string?[columns.Count];

            for (var ordinal = 0; ordinal < values.Length; ordinal++)
            {
                values[ordinal] = reader.IsDBNull(ordinal) ? null : AdoValueText.From(reader.GetValue(ordinal));
            }

            rows.Add(values);
        }

        return new QueryResultSet(columns, rows, isTruncated);
    }

    private async Task<T> QueryAsync<T>(
        Func<DbConnection, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        // 門を待つ前の早い弾き。ここを通れても、待っている間に閉じられることはある。
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 門を取れた時点で閉じ始めていたら、接続を触る前にここで弾く。
            // 上のチェックを通ってから門を待つ間に破棄が割り込むと、破棄が先に門を取って
            // 接続を閉じてしまうため、門の内側でもう一度見ないと破棄済みの接続を使ってしまう。
            ObjectDisposedException.ThrowIf(_disposed, this);

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
