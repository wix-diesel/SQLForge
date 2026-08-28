using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Filtering;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Explorer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ツリーの絞り込み。SSMS と同じで、見出しを右クリックして条件を決めると、
/// その見出しの下が条件に当てはまるものだけになり、見出しに「(フィルター適用)」が付く。
/// </summary>
public class ObjectExplorerFilterTests
{
    [Fact]
    public async Task 名前で絞り込むと当てはまるものだけが残る()
    {
        var editor = new StubFilterEditor(NameContains("or"));
        var explorer = NewExplorer(editor);
        var tables = await TablesAsync(explorer);

        Assert.Equal(["customers", "Invoices", "orders"], tables.Children.Select(node => node.Title));

        await tables.EditFilterCommand.ExecuteAsync(null);

        Assert.Equal(["orders"], tables.Children.Select(node => node.Title));

        // 件数も絞り込んだあとの数にする（3 と出ているのに 1 行しか無い、を避ける）。
        Assert.Equal("1", tables.Detail);
        Assert.True(tables.IsFiltered);
        Assert.Equal("テーブル (フィルター適用)", tables.DisplayTitle);
    }

    [Fact]
    public async Task 設定にはどの見出しかと条件にできるプロパティが渡る()
    {
        var editor = new StubFilterEditor(NameContains("or"));
        var explorer = NewExplorer(editor);
        var tables = await TablesAsync(explorer);

        await tables.EditFilterCommand.ExecuteAsync(null);

        Assert.Equal("sales_db/dbo/テーブル", editor.Path);
        Assert.Equal([ObjectFilterProperty.Name, ObjectFilterProperty.CreatedAt], editor.Properties);

        // 開き直したときは、今かかっている条件が渡る。
        await tables.EditFilterCommand.ExecuteAsync(null);
        Assert.False(editor.Current!.IsEmpty);
    }

    [Fact]
    public async Task 作成日でも絞り込める()
    {
        var editor = new StubFilterEditor(new ObjectFilter(
            [], new DateFilterClause(DateFilterOperator.GreaterThanOrEqual, new DateOnly(2026, 4, 1))));
        var explorer = NewExplorer(editor);
        var tables = await TablesAsync(explorer);

        await tables.EditFilterCommand.ExecuteAsync(null);

        // orders は 2026/04/15、customers は 2025/01/10、Invoices は作成日を読めていない。
        Assert.Equal(["orders"], tables.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task フィルターの削除で全部戻る()
    {
        var editor = new StubFilterEditor(NameContains("or"));
        var explorer = NewExplorer(editor);
        var tables = await TablesAsync(explorer);

        // 掛かっていないうちは「フィルターの削除」を押せない。
        Assert.False(tables.RemoveFilterCommand.CanExecute(null));

        await tables.EditFilterCommand.ExecuteAsync(null);
        Assert.True(tables.RemoveFilterCommand.CanExecute(null));

        await tables.RemoveFilterCommand.ExecuteAsync(null);

        Assert.Equal(["customers", "Invoices", "orders"], tables.Children.Select(node => node.Title));
        Assert.False(tables.IsFiltered);
        Assert.Equal("テーブル", tables.DisplayTitle);
        Assert.Equal("3", tables.Detail);
    }

    [Fact]
    public async Task 当てはまるものが無ければ空の行を出す()
    {
        var editor = new StubFilterEditor(NameContains("見つからない名前"));
        var explorer = NewExplorer(editor);
        var tables = await TablesAsync(explorer);

        await tables.EditFilterCommand.ExecuteAsync(null);

        var message = Assert.IsType<MessageNode>(Assert.Single(tables.Children));
        Assert.Equal("（なし）", message.Title);
        Assert.False(message.IsFailure);
        Assert.Equal("0", tables.Detail);
    }

    [Fact]
    public async Task 設定をやめると条件は変わらない()
    {
        var editor = new StubFilterEditor(result: null);
        var explorer = NewExplorer(editor);
        var tables = await TablesAsync(explorer);

        await tables.EditFilterCommand.ExecuteAsync(null);

        Assert.False(tables.IsFiltered);
        Assert.Equal(["customers", "Invoices", "orders"], tables.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task 絞り込みを掛けるとサーバーから読み直す()
    {
        // SSMS と同じく、条件を決めたら読み直す（前に読んだ一覧を切り取るだけにしない）。
        var session = NewSession();
        var editor = new StubFilterEditor(NameContains("sales"));
        var explorer = NewExplorer(editor, session);
        await explorer.InitializeAsync();

        var databases = (CatalogFolderNode)explorer.Roots[0].Children[0];
        var before = session.DatabaseCallCount;

        await databases.EditFilterCommand.ExecuteAsync(null);

        Assert.Equal(before + 1, session.DatabaseCallCount);
        Assert.Equal(["sales_db"], databases.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task 最新の情報に更新しても絞り込みは残る()
    {
        var editor = new StubFilterEditor(NameContains("or"));
        var explorer = NewExplorer(editor);
        var tables = await TablesAsync(explorer);

        await tables.EditFilterCommand.ExecuteAsync(null);
        await tables.RefreshCommand.ExecuteAsync(null);

        Assert.True(tables.IsFiltered);
        Assert.Equal(["orders"], tables.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task 行き先がつながっていなければフィルターのメニューは押せない()
    {
        // 押せるのに何も起きないメニューは出さない（ツリーだけを組む構成にはダイアログが無い）。
        var explorer = NewExplorer(filterEditor: null);
        var tables = await TablesAsync(explorer);

        Assert.False(tables.CanFilter);
        Assert.False(tables.EditFilterCommand.CanExecute(null));
        Assert.False(tables.RemoveFilterCommand.CanExecute(null));
    }

    [Fact]
    public async Task 列の見出しは絞り込めない()
    {
        // SSMS も列は絞り込めない。条件にできるものが無い見出しにはメニューを出さない。
        var explorer = NewExplorer(new StubFilterEditor(NameContains("id")));
        var tables = await TablesAsync(explorer);
        var orders = tables.Children.OfType<TableNode>().First(node => node.Title == "orders");

        await orders.EnsureChildrenAsync();
        var columns = (CatalogFolderNode)orders.Children.Single();

        Assert.Equal("列", columns.Title);
        Assert.False(columns.CanFilter);
    }

    [Fact]
    public async Task ユーザーの見出しは名前だけで絞り込む()
    {
        // 主体は作成日を読んでいないので、条件は名前だけにする。
        var editor = new StubFilterEditor(NameContains("app"));
        var explorer = NewExplorer(editor);
        await explorer.InitializeAsync();

        var database = explorer.Roots[0].Children[0].Children.OfType<DatabaseNode>()
            .First(node => node.Title == "sales_db");
        await database.EnsureChildrenAsync();

        var security = database.Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        var users = (DatabaseUsersNode)security.Children.First(node => node.Title == "ユーザー");
        await users.EnsureChildrenAsync();

        await users.EditFilterCommand.ExecuteAsync(null);

        Assert.Equal([ObjectFilterProperty.Name], editor.Properties);
        Assert.Equal("sales_db/セキュリティ/ユーザー", editor.Path);
        Assert.Equal(["app_user"], users.Children.Select(node => node.Title));
        Assert.Equal("ユーザー (フィルター適用)", users.DisplayTitle);
    }

    /// <summary>ダイアログの代わり。渡された条件をそのまま返し、何を訊かれたかを覚えておく。</summary>
    private sealed class StubFilterEditor(ObjectFilter? result) : IObjectFilterEditor
    {
        public string? Path { get; private set; }

        public IReadOnlyList<ObjectFilterProperty> Properties { get; private set; } = [];

        public ObjectFilter? Current { get; private set; }

        public Task<ObjectFilter?> EditAsync(
            string path,
            IReadOnlyList<ObjectFilterProperty> properties,
            ObjectFilter current)
        {
            Path = path;
            Properties = properties;
            Current = current;

            return Task.FromResult(result);
        }
    }

    private static ObjectFilter NameContains(string value) =>
        new([new TextFilterClause(ObjectFilterProperty.Name, TextFilterOperator.Contains, value)]);

    /// <summary>「sales_db → dbo → テーブル」まで開いて、テーブルの見出しを返す。</summary>
    private static async Task<CatalogFolderNode> TablesAsync(ObjectExplorerViewModel explorer)
    {
        await explorer.InitializeAsync();

        var database = explorer.Roots[0].Children[0].Children.OfType<DatabaseNode>()
            .First(node => node.Title == "sales_db");
        await database.EnsureChildrenAsync();

        var schemas = database.Children.First(node => node.Title == "スキーマ");
        await schemas.EnsureChildrenAsync();

        var dbo = schemas.Children.First(node => node.Title == "dbo");
        await dbo.EnsureChildrenAsync();

        var tables = (CatalogFolderNode)dbo.Children.First(node => node.Title == "テーブル");
        await tables.EnsureChildrenAsync();

        return tables;
    }

    private static ObjectExplorerViewModel NewExplorer(
        IObjectFilterEditor? filterEditor,
        FakeDatabaseSession? session = null) =>
        new(new CatalogContext(
            session ?? NewSession(),
            new ListDatabasesUseCase(),
            new ListSchemasUseCase(),
            new ListTablesUseCase(),
            new ListColumnsUseCase(),
            new ListStoredProceduresUseCase(),
            new ListStoredProcedureParametersUseCase())
        {
            FilterEditor = filterEditor,
            Security = new DatabaseSecurityContext(new ListDatabaseUsersUseCase())
        });

    private static FakeDatabaseSession NewSession()
    {
        var dbo = new SchemaName("dbo");

        return new FakeDatabaseSession
        {
            Databases =
            [
                new DatabaseDescriptor(new DatabaseName("sales_db"), CreatedAt: new DateTime(2026, 4, 15)),
                new DatabaseDescriptor(new DatabaseName("master"), IsSystem: true)
            ]
        }
        .WithDatabaseUsers("sales_db",
            new DatabaseUserDescriptor(new DatabaseUserName("app_user"), DatabaseUserType.SqlUserWithLogin),
            new DatabaseUserDescriptor(new DatabaseUserName("dbo"), DatabaseUserType.SqlUserWithLogin, IsSystem: true))
        .WithSchemas("sales_db", new SchemaDescriptor(dbo))
        .WithTables("sales_db", "dbo",
            new TableDescriptor(dbo, "orders", 8_400_000, new DateTime(2026, 4, 15)),
            new TableDescriptor(dbo, "customers", 120, new DateTime(2025, 1, 10)),
            new TableDescriptor(dbo, "Invoices", 0))
        .WithColumns("sales_db", "dbo", "orders",
            new ColumnDescriptor("id", 1, "int", IsNullable: false, IsIdentity: true, IsPrimaryKey: true));
    }
}
