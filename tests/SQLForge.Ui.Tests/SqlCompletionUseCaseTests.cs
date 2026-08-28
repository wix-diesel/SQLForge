using SQLForge.Application.Catalog;
using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 補完の候補作り。どこで何を出すかは SqlCompletionAnalyzer が決め、
/// ここはその種類に応じてカタログを引く。
/// </summary>
public class SqlCompletionUseCaseTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public async Task FROMの後ろにはスキーマとテーブルが並ぶ()
    {
        const string sql = "SELECT * FROM ";

        var result = await Complete(sql, sql.Length);

        Assert.Contains(result.Items, item => item is { Label: "dbo.orders", Kind: SqlCompletionItemKind.Table });
        Assert.Contains(result.Items, item => item is { Label: "dbo", Kind: SqlCompletionItemKind.Schema });

        // システムスキーマは候補に出さない。
        Assert.DoesNotContain(result.Items, item => item.Label == "sys");
    }

    [Fact]
    public async Task 別名の後ろにはそのテーブルの列が出る()
    {
        const string sql = "SELECT o. FROM dbo.orders AS o";

        var result = await Complete(sql, "SELECT o.".Length);

        Assert.Equal(["id", "region"], result.Items.Select(item => item.Label));
        Assert.All(result.Items, item => Assert.Equal(SqlCompletionItemKind.Column, item.Kind));
        Assert.Equal("SELECT o.".Length, result.ReplaceOffset);
        Assert.Equal(0, result.ReplaceLength);
    }

    [Fact]
    public async Task 列には持ち主と型を添える()
    {
        const string sql = "SELECT o. FROM dbo.orders AS o";

        var result = await Complete(sql, "SELECT o.".Length);

        Assert.Equal("o · int", result.Items[0].Detail);
    }

    [Fact]
    public async Task スキーマ名の後ろはそのスキーマのテーブルだけを出す()
    {
        const string sql = "SELECT * FROM dbo.";

        var result = await Complete(sql, sql.Length);

        Assert.Equal(["customers", "orders", "注文 一覧"], result.Items.Select(item => item.Label));
    }

    [Fact]
    public async Task 打ちかけの語で絞り込む()
    {
        const string sql = "SELECT * FROM ord";

        var result = await Complete(sql, sql.Length);

        Assert.Contains(result.Items, item => item.Label == "dbo.orders");
        Assert.DoesNotContain(result.Items, item => item.Label == "dbo.customers");
        Assert.Equal("SELECT * FROM ".Length, result.ReplaceOffset);
        Assert.Equal(3, result.ReplaceLength);
    }

    [Fact]
    public async Task そのままでは通らない名前には角かっこを付けて差し込む()
    {
        const string sql = "SELECT * FROM 注文";

        var result = await Complete(sql, sql.Length);

        var item = Assert.Single(result.Items, candidate => candidate.Label == "dbo.注文 一覧");
        Assert.Equal("dbo.[注文 一覧]", item.InsertText);
    }

    [Fact]
    public async Task 修飾なしの位置では列と予約語をどちらも出す()
    {
        const string sql = "SELECT  FROM dbo.orders";

        var result = await Complete(sql, "SELECT ".Length);

        Assert.Contains(result.Items, item => item is { Label: "region", Kind: SqlCompletionItemKind.Column });
        Assert.Contains(result.Items, item => item is { Label: "WHERE", Kind: SqlCompletionItemKind.Keyword });

        // 列を先に出す（予約語より当てが強いため）。
        Assert.Equal(SqlCompletionItemKind.Column, result.Items[0].Kind);
    }

    [Fact]
    public async Task 文字列の中では候補を返さない()
    {
        const string sql = "SELECT * FROM t WHERE name = 'ord";

        var result = await Complete(sql, sql.Length);

        Assert.True(result.IsEmpty);
    }

    private static Task<SqlCompletionResult> Complete(string sql, int caret)
    {
        var session = new FakeDatabaseSession()
            .WithSchemas(
                "sales_db",
                new SchemaDescriptor(new SchemaName("dbo")),
                new SchemaDescriptor(new SchemaName("sys"), IsSystem: true))
            .WithTables(
                "sales_db",
                "dbo",
                new TableDescriptor(new SchemaName("dbo"), "orders"),
                new TableDescriptor(new SchemaName("dbo"), "customers"),
                new TableDescriptor(new SchemaName("dbo"), "注文 一覧"))
            .WithTables("sales_db", "sys", new TableDescriptor(new SchemaName("sys"), "objects"))
            .WithColumns(
                "sales_db",
                "dbo",
                "orders",
                new ColumnDescriptor("id", 1, "int", IsNullable: false, IsIdentity: true, IsPrimaryKey: true),
                new ColumnDescriptor("region", 2, "nvarchar(50)", IsNullable: true, IsIdentity: false, IsPrimaryKey: false));

        var cache = new SchemaCache(
            session, new ListSchemasUseCase(), new ListTablesUseCase(), new ListColumnsUseCase());

        return new SqlCompletionUseCase(cache).ExecuteAsync(SalesDb, sql, caret);
    }
}
