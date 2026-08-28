using SQLForge.Application.Catalog;
using SQLForge.Domain.Catalog;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 補完のためのカタログの覚え書き。エディタは 1 文字打つたびに候補を求めるので、
/// 前の問い合わせが返る前に次が始まる（＝同時に走る）ことがある。
/// </summary>
public class SchemaCacheTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");
    private static readonly SchemaName Dbo = new("dbo");

    [Fact]
    public async Task 同じところを二度読みに行かない()
    {
        var session = NewSession(tableCount: 3);
        var cache = NewCache(session);

        var first = await cache.TablesAsync(SalesDb, Dbo);
        var second = await cache.TablesAsync(SalesDb, Dbo);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task 忘れさせると読み直す()
    {
        var session = NewSession(tableCount: 3);
        var cache = NewCache(session);

        var first = await cache.TablesAsync(SalesDb, Dbo);
        cache.Forget();

        Assert.NotSame(first, await cache.TablesAsync(SalesDb, Dbo));
    }

    [Fact]
    public async Task 同時に問い合わせても壊れない()
    {
        // 補完の要求が重なると、貯め込みが別々のスレッドから同時に書かれる。
        // 取り合いの起き方は実行のたびに変わるので、空の覚え書きから何度も攻める。
        const int tableCount = 40;
        var session = NewSession(tableCount);

        foreach (var _ in Enumerable.Range(0, 5))
        {
            var cache = NewCache(session);

            var work = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
            {
                var found = 0;

                foreach (var table in await cache.AllTablesAsync(SalesDb))
                {
                    found += (await cache.ColumnsAsync(SalesDb, Dbo, table.Name)).Count;
                }

                return found;
            }));

            var counts = await Task.WhenAll(work);

            // 1 テーブルにつき 2 列。どの要求も同じ数を見ているはず。
            Assert.All(counts, count => Assert.Equal(tableCount * 2, count));
        }
    }

    private static SchemaCache NewCache(FakeDatabaseSession session) =>
        new(session, new ListSchemasUseCase(), new ListTablesUseCase(), new ListColumnsUseCase());

    private static FakeDatabaseSession NewSession(int tableCount)
    {
        var session = new FakeDatabaseSession()
            .WithSchemas("sales_db", new SchemaDescriptor(Dbo))
            .WithTables(
                "sales_db",
                "dbo",
                [.. Enumerable.Range(0, tableCount).Select(index => new TableDescriptor(Dbo, $"t{index}"))]);

        foreach (var index in Enumerable.Range(0, tableCount))
        {
            session.WithColumns(
                "sales_db",
                "dbo",
                $"t{index}",
                new ColumnDescriptor("id", 1, "int", IsNullable: false, IsIdentity: true, IsPrimaryKey: true),
                new ColumnDescriptor("name", 2, "nvarchar(50)", IsNullable: true, IsIdentity: false, IsPrimaryKey: false));
        }

        return session;
    }
}
