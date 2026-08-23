using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Query;
using SQLForge.Infrastructure.Connections;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>実行の前に通す関門（空文・読み取り専用・取得上限）。</summary>
public class ExecuteQueryUseCaseTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public async Task 文面と実行先をそのままセッションへ渡す()
    {
        var session = ReadWriteSession();
        var useCase = new ExecuteQueryUseCase();

        await useCase.ExecuteAsync(session, new QueryRequest(SalesDb, "  SELECT 1  "));

        Assert.Equal("SELECT 1", session.ExecutedSql);
        Assert.Equal("sales_db", session.ExecutedDatabase);
        Assert.Equal(ExecuteQueryUseCase.DefaultMaxRows, session.ExecutedMaxRows);
    }

    [Fact]
    public async Task 空の文面は送らない()
    {
        var session = ReadWriteSession();

        await Assert.ThrowsAsync<QueryRejectedException>(
            () => new ExecuteQueryUseCase().ExecuteAsync(session, new QueryRequest(SalesDb, "   \n  ")));

        Assert.Null(session.ExecutedSql);
    }

    [Fact]
    public async Task 読み取り専用の接続では書き込みを送らない()
    {
        // 見本データの先頭は本番タグの読み取り専用接続。
        var session = new FakeDatabaseSession();
        Assert.True(session.Profile.IsReadOnly);

        var rejected = await Assert.ThrowsAsync<QueryRejectedException>(
            () => new ExecuteQueryUseCase().ExecuteAsync(
                session,
                new QueryRequest(SalesDb, "DELETE FROM dbo.orders")));

        Assert.Contains("DELETE", rejected.Message, StringComparison.Ordinal);
        Assert.Null(session.ExecutedSql);
    }

    [Fact]
    public async Task 読み書きの接続では書き込みを通す()
    {
        var session = ReadWriteSession();

        await new ExecuteQueryUseCase().ExecuteAsync(
            session,
            new QueryRequest(SalesDb, "DELETE FROM dbo.orders"));

        Assert.Equal("DELETE FROM dbo.orders", session.ExecutedSql);
    }

    [Fact]
    public async Task 取得上限は最低でも1行にする()
    {
        var session = ReadWriteSession();

        await new ExecuteQueryUseCase().ExecuteAsync(session, new QueryRequest(SalesDb, "SELECT 1", maxRows: 0));

        Assert.Equal(1, session.ExecutedMaxRows);
    }

    /// <summary>書き込みを許した接続。見本データの中から読み書きのものを選ぶ。</summary>
    private static FakeDatabaseSession ReadWriteSession()
    {
        var profile = SeedConnections.Create().First(candidate => candidate.AccessMode == AccessMode.ReadWrite);

        return new FakeDatabaseSession(profile)
        {
            NextResult = new QueryResult([], -1, TimeSpan.Zero)
        };
    }
}
