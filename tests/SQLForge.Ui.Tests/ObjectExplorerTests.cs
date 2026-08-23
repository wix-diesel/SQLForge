using SQLForge.Application.Catalog;
using SQLForge.Domain.Catalog;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Workspace;
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
        var tables = await ExpandAsync(schema, "テーブル");

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
        var tables = await ExpandAsync(schemas.Children.First(node => node.Title == "dbo"), "テーブル");

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
    public async Task 読み直しに失敗したら件数表示を消す()
    {
        var session = NewSession();
        var explorer = NewExplorer(session);
        await explorer.InitializeAsync();

        var databases = (CatalogFolderNode)explorer.Roots[0].Children[0];
        Assert.Equal("3", databases.Detail);

        session.Failure = new InvalidOperationException("権限がありません。");
        await databases.ReloadAsync();

        // 子が失敗の 1 行だけになったのに「3」が残ると、件数と中身が食い違って見える。
        Assert.Null(databases.Detail);
        Assert.IsType<MessageNode>(Assert.Single(databases.Children));
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

    [Fact]
    public async Task 作業領域がつながっていなければクエリのメニューは押せない()
    {
        // 押せるのに何も起きないメニューは出さない。ツリーだけを組む構成には行き先が無い。
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var database = Database(explorer, "sales_db");
        Assert.False(database.OpenQueryCommand.CanExecute(null));

        var schemas = await ExpandAsync(database);
        var tables = await ExpandAsync(schemas.Children.First(node => node.Title == "dbo"), "テーブル");

        Assert.False(tables.Children.OfType<TableNode>().First().OpenQueryCommand.CanExecute(null));
    }

    [Fact]
    public async Task 作業領域がつながっていればテーブルからクエリを開ける()
    {
        var launcher = new StubLauncher();
        var explorer = NewExplorer(NewSession(), launcher);
        await explorer.InitializeAsync();

        var database = Database(explorer, "sales_db");
        Assert.True(database.OpenQueryCommand.CanExecute(null));

        var schemas = await ExpandAsync(database);
        var tables = await ExpandAsync(schemas.Children.First(node => node.Title == "dbo"), "テーブル");
        var orders = tables.Children.OfType<TableNode>().First(node => node.Title == "orders");

        Assert.True(orders.OpenQueryCommand.CanExecute(null));
        orders.OpenQueryCommand.Execute(null);

        // テーブルが渡すのは、そのテーブルのあるデータベースだけ。
        Assert.Equal("sales_db", launcher.Opened?.Value);
    }

    [Fact]
    public async Task 開けないデータベースではクエリのメニューを押せない()
    {
        var explorer = NewExplorer(NewSession(), new StubLauncher());
        await explorer.InitializeAsync();

        // restoring_db は復元中でアクセスできない見本。
        Assert.False(Database(explorer, "restoring_db").OpenQueryCommand.CanExecute(null));
    }

    [Fact]
    public async Task 先頭1000件を表示すると角括弧で囲んだTOPの文面が実行される()
    {
        var launcher = new StubLauncher();
        var explorer = NewExplorer(NewSession(), launcher);
        await explorer.InitializeAsync();

        var database = Database(explorer, "sales_db");
        var schemas = await ExpandAsync(database);
        var tables = await ExpandAsync(schemas.Children.First(node => node.Title == "dbo"), "テーブル");
        var orders = tables.Children.OfType<TableNode>().First(node => node.Title == "orders");

        Assert.True(orders.ShowTopRowsCommand.CanExecute(null));
        orders.ShowTopRowsCommand.Execute(null);

        Assert.Equal("sales_db", launcher.RanFor?.Value);
        Assert.Equal("SELECT TOP (1000) * FROM [dbo].[orders];", launcher.RanSql);
    }

    [Fact]
    public async Task テーブルを開くと列の見出しの下にカラム定義が並ぶ()
    {
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var schemas = await ExpandAsync(Database(explorer, "sales_db"));
        var tables = await ExpandAsync(schemas.Children.First(node => node.Title == "dbo"), "テーブル");
        var orders = tables.Children.OfType<TableNode>().First(node => node.Title == "orders");

        var columns = await ExpandAsync(orders);

        Assert.Equal("列", columns.Title);
        Assert.Equal(["id", "customer_id"], columns.Children.Select(node => node.Title));

        var id = Assert.IsType<ColumnNode>(columns.Children.First(node => node.Title == "id"));
        Assert.True(id.IsPrimaryKey);
        Assert.Equal("PK, IDENTITY, int, not null", id.Detail);

        var customerId = Assert.IsType<ColumnNode>(columns.Children.First(node => node.Title == "customer_id"));
        Assert.False(customerId.IsPrimaryKey);
        Assert.Equal("int, null", customerId.Detail);
    }

    [Fact]
    public async Task スキーマを開くとテーブルとストアドプロシージャが並ぶ()
    {
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var schemas = await ExpandAsync(Database(explorer, "sales_db"));
        var schema = Assert.IsType<SchemaNode>(schemas.Children.First(node => node.Title == "dbo"));
        await schema.EnsureChildrenAsync();

        Assert.Equal(["テーブル", "ストアド プロシージャ"], schema.Children.Select(node => node.Title));

        var procedures = schema.Children.First(node => node.Title == "ストアド プロシージャ");
        await procedures.EnsureChildrenAsync();

        Assert.Equal(["usp_cancel_order", "usp_place_order"], procedures.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task ストアドプロシージャを開くとパラメーターの見出しの下に並ぶ()
    {
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var schemas = await ExpandAsync(Database(explorer, "sales_db"));
        var procedures = await ExpandAsync(
            schemas.Children.First(node => node.Title == "dbo"), "ストアド プロシージャ");
        var placeOrder = Assert.IsType<StoredProcedureNode>(
            procedures.Children.First(node => node.Title == "usp_place_order"));

        Assert.Equal("パラメーター 2", placeOrder.Detail);

        var parameters = await ExpandAsync(placeOrder);

        Assert.Equal("パラメーター", parameters.Title);
        Assert.Equal(["@customer_id", "@order_id"], parameters.Children.Select(node => node.Title));

        var orderId = Assert.IsType<StoredProcedureParameterNode>(
            parameters.Children.First(node => node.Title == "@order_id"));
        Assert.True(orderId.IsOutput);
        Assert.Equal("int, OUTPUT", orderId.Detail);
    }

    [Fact]
    public async Task 実行すると角括弧で囲んだEXECの文面が実行される()
    {
        var launcher = new StubLauncher();
        var explorer = NewExplorer(NewSession(), launcher);
        await explorer.InitializeAsync();

        var schemas = await ExpandAsync(Database(explorer, "sales_db"));
        var procedures = await ExpandAsync(
            schemas.Children.First(node => node.Title == "dbo"), "ストアド プロシージャ");
        var placeOrder = procedures.Children.OfType<StoredProcedureNode>().First(node => node.Title == "usp_place_order");

        Assert.True(placeOrder.ExecuteCommand.CanExecute(null));
        placeOrder.ExecuteCommand.Execute(null);

        Assert.Equal("sales_db", launcher.RanFor?.Value);
        Assert.Equal("EXEC [dbo].[usp_place_order];", launcher.RanSql);
    }

    [Fact]
    public void 定義を表示すると文字列リテラル内の引用符を二重化する()
    {
        // 区切り識別子はスキーマ名・プロシージャ名に ' を含められる。角括弧の引用符付けとは
        // 別に、文字列リテラルへ埋め込むところでも二重化しないと文が途中で終わってしまう。
        var launcher = new StubLauncher();
        var context = new CatalogContext(
            new FakeDatabaseSession(),
            new ListDatabasesUseCase(),
            new ListSchemasUseCase(),
            new ListTablesUseCase(),
            new ListColumnsUseCase(),
            new ListStoredProceduresUseCase(),
            new ListStoredProcedureParametersUseCase(),
            launcher);

        var node = new StoredProcedureNode(
            context,
            new DatabaseName("sales_db"),
            new StoredProcedureDescriptor(new SchemaName("weird's"), "usp_o'rder"));

        node.ViewDefinitionCommand.Execute(null);

        Assert.Equal("sales_db", launcher.RanFor?.Value);
        Assert.Equal(
            "SELECT OBJECT_DEFINITION(OBJECT_ID(N'[weird''s].[usp_o''rder]')) AS definition;",
            launcher.RanSql);
    }

    [Fact]
    public async Task 作業領域がつながっていなければストアドプロシージャのメニューは押せない()
    {
        var explorer = NewExplorer(NewSession());
        await explorer.InitializeAsync();

        var schemas = await ExpandAsync(Database(explorer, "sales_db"));
        var procedures = await ExpandAsync(
            schemas.Children.First(node => node.Title == "dbo"), "ストアド プロシージャ");
        var placeOrder = procedures.Children.OfType<StoredProcedureNode>().First(node => node.Title == "usp_place_order");

        Assert.False(placeOrder.ExecuteCommand.CanExecute(null));
        Assert.False(placeOrder.ViewDefinitionCommand.CanExecute(null));
    }

    /// <summary>行き先があることだけを表す差し込み。開かれたデータベースを覚えておく。</summary>
    private sealed class StubLauncher : IQueryLauncher
    {
        public DatabaseName? Opened { get; private set; }
        public DatabaseName? RanFor { get; private set; }
        public string? RanSql { get; private set; }

        public void OpenNewQuery(DatabaseName database) => Opened = database;

        public void OpenAndRunQuery(DatabaseName database, string sql)
        {
            RanFor = database;
            RanSql = sql;
        }
    }

    private static ObjectExplorerViewModel NewExplorer(
        FakeDatabaseSession session,
        IQueryLauncher? query = null) =>
        new(new CatalogContext(
            session,
            new ListDatabasesUseCase(),
            new ListSchemasUseCase(),
            new ListTablesUseCase(),
            new ListColumnsUseCase(),
            new ListStoredProceduresUseCase(),
            new ListStoredProcedureParametersUseCase(),
            query));

    private static DatabaseNode Database(ObjectExplorerViewModel explorer, string name) =>
        explorer.Roots[0].Children[0].Children.OfType<DatabaseNode>().First(node => node.Title == name);

    /// <summary>
    /// ノードを展開し、その下の見出しノードを開いて返す。子の見出しが 1 つしかないなら
    /// <paramref name="folderTitle"/> は省いてよい（スキーマは「テーブル」「ストアド プロシージャ」の
    /// 2 つを持つので、そこだけは名指しで選ぶ）。
    /// </summary>
    private static async Task<ObjectExplorerNode> ExpandAsync(ObjectExplorerNode node, string? folderTitle = null)
    {
        await node.EnsureChildrenAsync();
        var folder = folderTitle is null ? node.Children.Single() : node.Children.First(child => child.Title == folderTitle);
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
            new TableDescriptor(dbo, "Invoices", 0))
        .WithColumns("sales_db", "dbo", "orders",
            new ColumnDescriptor("id", 1, "int", IsNullable: false, IsIdentity: true, IsPrimaryKey: true),
            new ColumnDescriptor("customer_id", 2, "int", IsNullable: true, IsIdentity: false, IsPrimaryKey: false))
        .WithStoredProcedures("sales_db", "dbo",
            new StoredProcedureDescriptor(dbo, "usp_place_order", 2),
            new StoredProcedureDescriptor(dbo, "usp_cancel_order", 0))
        .WithStoredProcedureParameters("sales_db", "dbo", "usp_place_order",
            new StoredProcedureParameterDescriptor("@customer_id", 1, "int", IsOutput: false, HasDefaultValue: false),
            new StoredProcedureParameterDescriptor("@order_id", 2, "int", IsOutput: true, HasDefaultValue: false));
    }
}
