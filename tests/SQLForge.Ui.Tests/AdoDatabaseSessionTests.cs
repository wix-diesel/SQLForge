using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// セッションの後始末。ツリーは複数のノードを同時に展開できるので、
/// 照会の最中にウィンドウを閉じられる状況が普通に起きる。
/// </summary>
public class AdoDatabaseSessionTests
{
    [Fact]
    public async Task 照会の最中に閉じても例外にならず接続は閉じる()
    {
        var connection = new StubConnection();
        var session = new BlockingSession(connection);

        var query = session.ListDatabasesAsync();
        await session.Started.Task;

        // 照会が門を握ったままウィンドウが閉じられる、という並びを作る。
        var dispose = session.DisposeAsync();
        session.Finish.SetResult();

        await query;
        await dispose;

        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task 閉じ終わるまで接続を破棄しない()
    {
        // 読み取り中に接続が消えると DbDataReader の足元が崩れる。
        var connection = new StubConnection();
        var session = new BlockingSession(connection);

        var query = session.ListDatabasesAsync();
        await session.Started.Task;

        var dispose = session.DisposeAsync().AsTask();
        await Task.WhenAny(dispose, Task.Delay(100));

        Assert.False(dispose.IsCompleted);
        Assert.False(connection.IsDisposed);

        session.Finish.SetResult();
        await query;
        await dispose;

        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task 門を待っている間に閉じられた照会は接続を触らずに弾く()
    {
        // 先の照会が門を握っている間に次の照会が並び、その状態でウィンドウが閉じられる、という並び。
        // 門を待つ前のチェックはすでに通過しているので、門の内側で見直さないと
        // 破棄されようとしている接続をそのまま使ってしまう。
        var connection = new StubConnection();
        var session = new BlockingSession(connection);

        var running = session.ListDatabasesAsync();
        await session.Started.Task;

        var queued = session.ListDatabasesAsync();       // 門待ちに並ぶ（破棄前なので早い弾きは通る）
        await session.WaitingAtGate();

        var dispose = session.DisposeAsync();            // ここで閉じ始める
        session.Finish.SetResult();                      // 先の照会が門を返す

        await running;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
        await dispose;

        // 弾かれた照会は接続まで届いていない。
        Assert.Equal(1, session.ReadCount);
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task 二度閉じても何も起きない()
    {
        var connection = new StubConnection();
        var session = new BlockingSession(connection);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task 閉じたあとの照会は弾く()
    {
        var session = new BlockingSession(new StubConnection());
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ListDatabasesAsync());
    }

    /// <summary>合図をもらうまで終わらない照会を持つセッション。接続そのものは使わない。</summary>
    private sealed class BlockingSession(DbConnection connection)
        : AdoDatabaseSession(
            SeedConnections.Create().First(),
            connection,
            new ServerInfo("SQL Server 2022", "16.0.4215.2"))
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>接続まで届いた照会の数。弾かれた照会はここに現れない。</summary>
        public int ReadCount { get; private set; }

        /// <summary>
        /// 次の照会が門待ちの列に並ぶまで待つ。門の内側へ入れるのは 1 本だけなので、
        /// 空きが 0 のまま数回譲れば、並び終えたとみなせる。
        /// </summary>
        public async Task WaitingAtGate()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await Task.Yield();
            }
        }

        protected override async Task<IReadOnlyList<DatabaseDescriptor>> ReadDatabasesAsync(
            DbConnection connection,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            Started.TrySetResult();
            await Finish.Task;

            return [];
        }

        protected override Task<IReadOnlyList<SchemaDescriptor>> ReadSchemasAsync(
            DbConnection connection,
            DatabaseName database,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SchemaDescriptor>>([]);

        protected override Task<IReadOnlyList<TableDescriptor>> ReadTablesAsync(
            DbConnection connection,
            DatabaseName database,
            SchemaName schema,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TableDescriptor>>([]);

        protected override Task<IReadOnlyList<ColumnDescriptor>> ReadColumnsAsync(
            DbConnection connection,
            DatabaseName database,
            SchemaName schema,
            string table,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ColumnDescriptor>>([]);

        protected override Task<IReadOnlyList<StoredProcedureDescriptor>> ReadStoredProceduresAsync(
            DbConnection connection,
            DatabaseName database,
            SchemaName schema,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredProcedureDescriptor>>([]);

        protected override Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ReadStoredProcedureParametersAsync(
            DbConnection connection,
            DatabaseName database,
            SchemaName schema,
            string procedure,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredProcedureParameterDescriptor>>([]);

        // クエリの実行はここでは試さない（門の作法だけを見るテストなので、接続には届かせない）。
        protected override Task SwitchDatabaseAsync(
            DbConnection connection,
            DatabaseName database,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>破棄されたかだけを覚えている接続。照会には使わないので他は実装しない。</summary>
    private sealed class StubConnection : DbConnection
    {
        public int DisposeCount { get; private set; }

        public bool IsDisposed => DisposeCount > 0;

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => IsDisposed ? ConnectionState.Closed : ConnectionState.Open;

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() => throw new NotSupportedException();

        public override void Open() => throw new NotSupportedException();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
