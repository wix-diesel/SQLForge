using SQLForge.Application.Catalog;
using SQLForge.Domain.Catalog;
using SQLForge.Ui.ViewModels.Explorer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// オブジェクトエクスプローラーの遅延読み込み。
/// 起動直後にデータベースまで見えること、テーブルはスキーマを開くまで読まないこと。
/// </summary>
public class ObjectExplorerTests
{
    [Fact]
    public async Task 起動直後に接続とデータベースの一覧が開いている()
    {
        var explorer = NewExplorer(NewSession());

        await explorer.InitializeAsync();

        var server = Assert.IsType<ServerNode>(Assert.Single(explorer.Roots));
        Assert.True(server.IsExpanded);
        Assert.Equal("SQL Server 2022 (16.0.4215.2)", server.Detail);

        var databases = Assert.IsType<CatalogFolderNode>(Assert.Single(server.Children));
        Assert.True(databases.IsExpanded);
        Assert.Equal("3", databases.Detail);
        Assert.Equal(["restoring_db", "sales_db", "master"], databases.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task スキーマを開くまでテーブルは読まない()
    {
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var database = Database(explorer, "sales_db");
        Assert.Equal("読み込み中…", Assert.Single(database.Children).Title);

        var schemas = await ExpandAsync(database);
        var schema = Assert.IsType<SchemaNode>(schemas.Children.First(node => node.Title == "dbo"));
        var tables = await ExpandAsync(schema);

        Assert.Equal("テーブル", tables.Title);
        Assert.Equal("3", tables.Detail);
        Assert.Equal(["customers", "Invoices", "orders"], tables.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task テーブルの行数は丸めて出す()
    {
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var schemas = await ExpandAsync(Database(explorer, "sales_db"));
        var tables = await ExpandAsync(schemas.Children.First(node => node.Title == "dbo"));

        Assert.Equal("8.4M 行", tables.Children.First(node => node.Title == "orders").Detail);
        Assert.Equal("120 行", tables.Children.First(node => node.Title == "customers").Detail);
    }

    [Fact]
    public async Task 開けないデータベースは展開させない()
    {
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var offline = Database(explorer, "restoring_db");

        Assert.False(offline.CanExpand);
        Assert.Equal("アクセスできません", offline.Detail);
        Assert.Empty(offline.Children);
    }

    [Fact]
    public async Task 読み込みに失敗したら理由を子の行に出してやり直せる()
    {
        var session = NewSession();
        session.Failure = new InvalidOperationException("権限がありません。");
        var explorer = NewExplorer(session);

        await explorer.InitializeAsync();

        var databases = (CatalogFolderNode)explorer.Roots[0].Children[0];
        var message = Assert.IsType<MessageNode>(Assert.Single(databases.Children));
        Assert.True(message.IsFailure);
        Assert.Equal("権限がありません。", message.Title);

        // 失敗は「読み込み済み」にしないので、開き直せばもう一度取りにいく。
        session.Failure = null;
        await databases.EnsureChildrenAsync();

        Assert.Equal(["restoring_db", "sales_db", "master"], databases.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task 読み直すとツリーを組み立て直す()
    {
        var session = NewSession();
        var explorer = NewExplorer(session);
        await explorer.InitializeAsync();
        var before = session.DatabaseCallCount;

        await explorer.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(before + 1, session.DatabaseCallCount);
        Assert.Null(explorer.SelectedNode);
        Assert.Equal("3", ((CatalogFolderNode)explorer.Roots[0].Children[0]).Detail);
    }

    private static ObjectExplorerViewModel NewExplorer(FakeDatabaseSession session) =>
        new(new CatalogContext(session, new ListDatabasesUseCase(), new ListSchemasUseCase(), new ListTablesUseCase()));

    private static DatabaseNode Database(ObjectExplorerViewModel explorer, string name) =>
        explorer.Roots[0].Children[0].Children.OfType<DatabaseNode>().First(node => node.Title == name);

    /// <summary>ノードを展開し、その下の見出しノードを開いて返す。</summary>
    private static async Task<ObjectExplorerNode> ExpandAsync(ObjectExplorerNode node)
    {
        await node.EnsureChildrenAsync();
        var folder = node.Children.Single();
        await folder.EnsureChildrenAsync();

        return folder;
    }

    private static FakeDatabaseSession NewSession()
    {
        var dbo = new SchemaName("dbo");

        return new FakeDatabaseSession
        {
            Databases =
            [
                new DatabaseDescriptor(new DatabaseName("sales_db")),
                new DatabaseDescriptor(new DatabaseName("master"), IsSystem: true),
                new DatabaseDescriptor(new DatabaseName("restoring_db"), IsAccessible: false)
            ]
        }
        .WithSchemas("sales_db",
            new SchemaDescriptor(dbo),
            new SchemaDescriptor(new SchemaName("sys"), IsSystem: true))
        .WithTables("sales_db", "dbo",
            new TableDescriptor(dbo, "orders", 8_400_000),
            new TableDescriptor(dbo, "customers", 120),
            new TableDescriptor(dbo, "Invoices", 0));
    }
}
