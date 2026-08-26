using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Editing;
using SQLForge.Domain.Query;
using SQLForge.Domain.Security;
using SQLForge.Infrastructure.Connections;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 編集グリッドの読み書きのうち、ドライバーに依らない部分。
///
/// ここは実サーバーが要る箇所に見えるが、要るのは ADO.NET の口だけなので、
/// 決め打ちの行を返す接続を差し込めば「打ち切りの見分け」と「1 行でない更新の巻き戻し」を
/// 機械的に確かめられる。どちらも間違えると、画面に出ていない行まで書き換わったり、
/// 続きがあることに気付けなかったりする。
/// </summary>
public class AdoDatabaseSessionEditingTests
{
    private static readonly DatabaseName ShopDb = new("shop");
    private static readonly SchemaName Dbo = new("dbo");

    [Fact]
    public async Task 上限より一行だけ多く求める()
    {
        // ちょうど上限で切る文面（SELECT TOP (100)）だと、上限まで返ってきたのが
        // 「たまたま上限ちょうど」なのか「まだ続きがある」のかを見分けられない。
        var connection = new FakeDbConnection();
        var session = new EditingSession(connection);

        await session.ReadEditableRowsAsync(ShopDb, Dbo, "orders", maxRows: 100);

        Assert.Equal(101, session.RequestedMaxRows);
    }

    [Fact]
    public async Task 上限を超えて返ってきたら打ち切りとして扱い余りは持たない()
    {
        var connection = new FakeDbConnection { Rows = Rows(4) };
        var session = new EditingSession(connection);

        var rows = await session.ReadEditableRowsAsync(ShopDb, Dbo, "orders", maxRows: 3);

        Assert.True(rows.IsTruncated);
        Assert.Equal(3, rows.Rows.Count);
        Assert.Equal(["1", "2", "3"], rows.Rows.Select(row => row[0]));
    }

    [Fact]
    public async Task ちょうど上限までなら打ち切りにしない()
    {
        var connection = new FakeDbConnection { Rows = Rows(3) };
        var session = new EditingSession(connection);

        var rows = await session.ReadEditableRowsAsync(ShopDb, Dbo, "orders", maxRows: 3);

        Assert.False(rows.IsTruncated);
        Assert.Equal(3, rows.Rows.Count);
    }

    [Fact]
    public async Task NULLは空文字列と区別して持つ()
    {
        var connection = new FakeDbConnection { Rows = [new object?[] { "1", null }] };
        var session = new EditingSession(connection);

        var rows = await session.ReadEditableRowsAsync(ShopDb, Dbo, "orders", maxRows: 3);

        Assert.Null(rows.Rows[0][1]);
    }

    [Fact]
    public async Task 一行だけに当たった更新はコミットする()
    {
        var connection = new FakeDbConnection { AffectedRows = 1 };
        var session = new EditingSession(connection);

        var affected = await session.UpdateTableCellAsync(ShopDb, Dbo, "orders", Update());

        Assert.Equal(1, affected);
        Assert.True(connection.Transaction!.IsCommitted);
        Assert.False(connection.Transaction.IsRolledBack);
    }

    [Fact]
    public async Task 一行も当たらなくてもコミットして件数をそのまま返す()
    {
        // 「行が無い」ことは巻き戻す対象ではない。読み直しを促すのは上の層の仕事。
        var connection = new FakeDbConnection { AffectedRows = 0 };
        var session = new EditingSession(connection);

        var affected = await session.UpdateTableCellAsync(ShopDb, Dbo, "orders", Update());

        Assert.Equal(0, affected);
        Assert.True(connection.Transaction!.IsCommitted);
    }

    [Fact]
    public async Task 二行以上に当たった更新は巻き戻す()
    {
        // 編集グリッドの操作は「画面のこの行を直す」なので、多くの行に当たった時点で別のことをしている。
        var connection = new FakeDbConnection { AffectedRows = 2 };
        var session = new EditingSession(connection);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.UpdateTableCellAsync(ShopDb, Dbo, "orders", Update()));

        Assert.Contains("2 行", failure.Message, StringComparison.Ordinal);
        Assert.True(connection.Transaction!.IsRolledBack);
        Assert.False(connection.Transaction.IsCommitted);
    }

    private static TableCellUpdate Update() =>
        new("status", "shipped", [new RowCriterion("id", "1")]);

    private static IReadOnlyList<IReadOnlyList<object?>> Rows(int count) =>
        Enumerable.Range(1, count)
            .Select(number => (IReadOnlyList<object?>)new object?[] { number.ToString(), "paid" })
            .ToList();

    /// <summary>
    /// 編集の口だけを実装したセッション。文面の組み立ては記録に置き換えて、
    /// 共通部分（求めた行数・読み取り・トランザクション）だけを見る。
    /// </summary>
    private sealed class EditingSession(DbConnection connection)
        : AdoDatabaseSession(
            SeedConnections.Create().First(),
            connection,
            new ServerInfo("SQL Server 2022", "16.0.4215.2"))
    {
        /// <summary>文面へ渡された行数。上限より 1 行多いことを確かめるのに使う。</summary>
        public int RequestedMaxRows { get; private set; }

        protected override Task<IReadOnlyList<EditableColumn>> ReadEditableColumnsAsync(
            DbConnection connection,
            DatabaseName database,
            SchemaName schema,
            string table,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EditableColumn>>(
            [
                new EditableColumn(
                    "id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false),
                new EditableColumn(
                    "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false,
                    IsText: true)
            ]);

        protected override ParameterizedStatement BuildTopRowsSelect(
            SchemaName schema,
            string table,
            IReadOnlyList<EditableColumn> columns,
            int maxRows)
        {
            RequestedMaxRows = maxRows;

            return new ParameterizedStatement("SELECT", [maxRows]);
        }

        protected override ParameterizedStatement BuildCellUpdate(
            SchemaName schema,
            string table,
            IReadOnlyList<EditableColumn> columns,
            TableCellUpdate update) =>
            new("UPDATE", [update.Value]);

        protected override Task SwitchDatabaseAsync(
            DbConnection connection,
            DatabaseName database,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task<IReadOnlyList<DatabaseDescriptor>> ReadDatabasesAsync(
            DbConnection connection,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DatabaseDescriptor>>([]);

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

        protected override Task<IReadOnlyList<DatabaseUserDescriptor>> ReadDatabaseUsersAsync(
            DbConnection connection,
            DatabaseName database,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DatabaseUserDescriptor>>([]);

        protected override Task<IReadOnlyList<string>> ReadDatabaseRolesAsync(
            DbConnection connection,
            DatabaseName database,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        protected override Task CreateUserAsync(
            DbConnection connection,
            DatabaseName database,
            DatabaseUserDefinition definition,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task AlterUserAsync(
            DbConnection connection,
            DatabaseName database,
            DatabaseUserDescriptor original,
            DatabaseUserDefinition definition,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task DropUserAsync(
            DbConnection connection,
            DatabaseName database,
            DatabaseUserName user,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task<IReadOnlyList<ServerLoginDescriptor>> ReadServerLoginsAsync(
            DbConnection connection,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ServerLoginDescriptor>>([]);

        protected override Task<IReadOnlyList<string>> ReadServerRolesAsync(
            DbConnection connection,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        protected override Task CreateLoginAsync(
            DbConnection connection,
            ServerLoginDefinition definition,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task AlterLoginAsync(
            DbConnection connection,
            ServerLoginDescriptor original,
            ServerLoginDefinition definition,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task DropLoginAsync(
            DbConnection connection,
            ServerLoginName login,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
