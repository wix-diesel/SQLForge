using Microsoft.Extensions.DependencyInjection;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Ui.Composition;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Explorer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// できることは DBMS ごとに違う。まだ書いていない操作をメニューや枝に出すと、
/// 利用者は押してから断られることになるので、工場がセッションの申告どおりに組むことを確かめる。
/// </summary>
public class MainWindowViewModelFactoryTests
{
    [Fact]
    public async Task カタログしか読めない接続にはセキュリティも編集グリッドも出さない()
    {
        var viewModel = Create(SessionCapabilities.CatalogOnly);
        await viewModel.InitializeAsync();

        var server = Assert.Single(viewModel.Explorer.Roots);
        Assert.Equal(["データベース"], server.Children.Select(node => node.Title));
        Assert.False((await TableAsync(viewModel)).CanEditRows);
    }

    [Fact]
    public async Task 全部そろった接続にはセキュリティも編集グリッドも出す()
    {
        var viewModel = Create(SessionCapabilities.Full);
        await viewModel.InitializeAsync();

        var server = Assert.Single(viewModel.Explorer.Roots);
        Assert.Contains("セキュリティ", server.Children.Select(node => node.Title));
        Assert.True((await TableAsync(viewModel)).CanEditRows);
    }

    /// <summary>ツリーを「データベース → sales_db → スキーマ → dbo → テーブル」と辿って 1 件返す。</summary>
    private static async Task<TableNode> TableAsync(MainWindowViewModel viewModel)
    {
        var database = viewModel.Explorer.Roots[0].Children[0].Children.OfType<DatabaseNode>()
            .First(node => node.Title == "sales_db");

        var schemas = await ExpandAsync(database, "スキーマ");
        var tables = await ExpandAsync(schemas.Children.First(node => node.Title == "dbo"), "テーブル");

        return tables.Children.OfType<TableNode>().First();
    }

    private static async Task<ObjectExplorerNode> ExpandAsync(ObjectExplorerNode node, string folderTitle)
    {
        await node.EnsureChildrenAsync();
        var folder = node.Children.First(child => child.Title == folderTitle);
        await folder.EnsureChildrenAsync();

        return folder;
    }

    private static MainWindowViewModel Create(SessionCapabilities capabilities)
    {
        var services = new ServiceCollection();
        AppServices.Configure(services);

        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<MainWindowViewModelFactory>().Create(NewSession(capabilities));
    }

    private static FakeDatabaseSession NewSession(SessionCapabilities capabilities)
    {
        var dbo = new SchemaName("dbo");

        return new FakeDatabaseSession
        {
            Capabilities = capabilities,
            Databases = [new DatabaseDescriptor(new DatabaseName("sales_db"))]
        }
        .WithSchemas("sales_db", new SchemaDescriptor(dbo))
        .WithTables("sales_db", "dbo", new TableDescriptor(dbo, "orders", 120));
    }
}
