using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ツリーのスキーマの枝。所有者が行に出ること、右クリックの追加・編集・削除が
/// ダイアログへつながり、済んだら読み直すこと。
/// </summary>
public class SchemaExplorerTests
{
    [Fact]
    public async Task スキーマは所有者を添えて並ぶ()
    {
        var schemas = await ExpandSchemasAsync(NewSession(), new StubEditor());

        Assert.Equal("2", schemas.Detail);

        var dbo = schemas.Children.OfType<SchemaNode>().First(node => node.Title == "dbo");
        Assert.Equal("所有者 dbo", dbo.Detail);
        Assert.Equal("dbo", dbo.Owner);
    }

    [Fact]
    public async Task システムのスキーマは編集も削除もさせない()
    {
        var schemas = await ExpandSchemasAsync(NewSession(), new StubEditor());
        var system = schemas.Children.OfType<SchemaNode>().First(node => node.Title == "sys");

        Assert.True(system.IsSystem);
        Assert.False(system.PropertiesCommand.CanExecute(null));
        Assert.False(system.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task 行き先がつながっていなければ追加も編集も押せない()
    {
        var schemas = await ExpandSchemasAsync(NewSession(), editor: null);

        Assert.False(schemas.NewSchemaCommand.CanExecute(null));
        Assert.False(schemas.Children.OfType<SchemaNode>().First().PropertiesCommand.CanExecute(null));
    }

    [Fact]
    public async Task 追加が済んだら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var schemas = await ExpandSchemasAsync(session, editor);

        await schemas.NewSchemaCommand.ExecuteAsync(null);

        Assert.Equal("sales_db", editor.CreatedFor?.Value);
        Assert.Equal(2, schemas.Children.Count);
    }

    [Fact]
    public async Task プロパティで所有者を変えたら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var schemas = await ExpandSchemasAsync(session, editor);
        var dbo = schemas.Children.OfType<SchemaNode>().First(node => node.Title == "dbo");

        await dbo.PropertiesCommand.ExecuteAsync(null);

        Assert.Equal("dbo", editor.EditedSchema?.Name.Value);
    }

    [Fact]
    public async Task 削除したら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var schemas = await ExpandSchemasAsync(session, editor);
        var dbo = schemas.Children.OfType<SchemaNode>().First(node => node.Title == "dbo");

        await dbo.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("dbo", editor.DeletedSchema?.Name.Value);
    }

    /// <summary>行き先があることだけを表す差し込み。何を渡されたかを覚えておく。</summary>
    private sealed class StubEditor : ISchemaEditor
    {
        public bool Result { get; init; }

        public DatabaseName? CreatedFor { get; private set; }

        public SchemaDescriptor? EditedSchema { get; private set; }

        public SchemaDescriptor? DeletedSchema { get; private set; }

        public Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database)
        {
            CreatedFor = database;
            return Task.FromResult(Result);
        }

        public Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, SchemaDescriptor schema)
        {
            EditedSchema = schema;
            return Task.FromResult(Result);
        }

        public Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, SchemaDescriptor schema)
        {
            DeletedSchema = schema;
            return Task.FromResult(Result);
        }
    }

    private static async Task<SchemasNode> ExpandSchemasAsync(FakeDatabaseSession session, ISchemaEditor? editor)
    {
        var explorer = NewExplorer(session, editor);
        await explorer.InitializeAsync();

        var database = explorer.Roots[0].Children[0].Children.OfType<DatabaseNode>().First();
        await database.EnsureChildrenAsync();

        var schemas = database.Children.OfType<SchemasNode>().Single();
        await schemas.EnsureChildrenAsync();

        return schemas;
    }

    private static ObjectExplorerViewModel NewExplorer(FakeDatabaseSession session, ISchemaEditor? editor)
    {
        var context = new CatalogContext(
            session,
            new ListDatabasesUseCase(),
            new ListSchemasUseCase(),
            new ListTablesUseCase(),
            new ListColumnsUseCase(),
            new ListStoredProceduresUseCase(),
            new ListStoredProcedureParametersUseCase())
        {
            Security = new DatabaseSecurityContext(new ListDatabaseUsersUseCase()) { SchemaEditor = editor }
        };

        return new ObjectExplorerViewModel(context);
    }

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession
        {
            Databases = [new DatabaseDescriptor(new DatabaseName("sales_db"))]
        }
        .WithSchemas("sales_db",
            new SchemaDescriptor(new SchemaName("dbo"), Owner: "dbo"),
            new SchemaDescriptor(new SchemaName("sys"), IsSystem: true, Owner: "sys"));
}
