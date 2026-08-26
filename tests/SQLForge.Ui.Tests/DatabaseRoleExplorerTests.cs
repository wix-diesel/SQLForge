using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ツリーの「セキュリティ → データベース ロール」の枝。SSMS と同じ並びで出ること、
/// 右クリックの追加・編集・削除がダイアログへつながり、済んだら読み直すこと。
/// </summary>
public class DatabaseRoleExplorerTests
{
    [Fact]
    public async Task セキュリティの下にユーザーとロールの見出しが並ぶ()
    {
        var explorer = NewExplorer(NewSession(), new StubEditor());
        await explorer.InitializeAsync();

        var database = Database(explorer);
        await database.EnsureChildrenAsync();

        var security = database.Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        Assert.Equal(["ユーザー", "データベース ロール"], security.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task ロールは名前順に所有者とメンバー数を添えて並ぶ()
    {
        var roles = await ExpandRolesAsync(NewSession(), new StubEditor());

        Assert.Equal("2", roles.Detail);
        Assert.Equal(["app_reader", "db_owner"], roles.Children.Select(node => node.Title));

        var appReader = Assert.IsType<DatabaseRoleNode>(roles.Children[0]);
        Assert.Equal("所有者 dbo · メンバー 1", appReader.Detail);
        Assert.False(appReader.IsSystem);

        var fixedRole = Assert.IsType<DatabaseRoleNode>(roles.Children[1]);
        Assert.True(fixedRole.IsSystem);
    }

    [Fact]
    public async Task 固定ロールはプロパティを開けるが削除はさせない()
    {
        // メンバーの出し入れは日常の操作なので、固定ロールでもプロパティは開ける。
        var roles = await ExpandRolesAsync(NewSession(), new StubEditor());
        var fixedRole = roles.Children.OfType<DatabaseRoleNode>().First(node => node.Title == "db_owner");

        Assert.True(fixedRole.PropertiesCommand.CanExecute(null));
        Assert.False(fixedRole.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task 行き先がつながっていなければ追加も編集も押せない()
    {
        var roles = await ExpandRolesAsync(NewSession(), editor: null);

        Assert.False(roles.NewRoleCommand.CanExecute(null));
        Assert.False(roles.Children.OfType<DatabaseRoleNode>().First().PropertiesCommand.CanExecute(null));
    }

    [Fact]
    public async Task 追加が済んだら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var roles = await ExpandRolesAsync(session, editor);
        var before = session.DatabaseRoleCallCount;

        await roles.NewRoleCommand.ExecuteAsync(null);

        Assert.Equal("sales_db", editor.CreatedFor?.Value);
        Assert.Equal(before + 1, session.DatabaseRoleCallCount);
    }

    [Fact]
    public async Task プロパティで変えたら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var roles = await ExpandRolesAsync(session, editor);
        var appReader = roles.Children.OfType<DatabaseRoleNode>().First(node => node.Title == "app_reader");
        var before = session.DatabaseRoleCallCount;

        await appReader.PropertiesCommand.ExecuteAsync(null);

        Assert.Equal("app_reader", editor.EditedRole?.Name.Value);
        Assert.Equal(before + 1, session.DatabaseRoleCallCount);
    }

    [Fact]
    public async Task 削除したら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var roles = await ExpandRolesAsync(session, editor);
        var appReader = roles.Children.OfType<DatabaseRoleNode>().First(node => node.Title == "app_reader");
        var before = session.DatabaseRoleCallCount;

        await appReader.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("app_reader", editor.DeletedRole?.Name.Value);
        Assert.Equal(before + 1, session.DatabaseRoleCallCount);
    }

    [Fact]
    public async Task 読み込みに失敗したら理由を子の行に出して件数を消す()
    {
        var session = NewSession();
        var roles = await ExpandRolesAsync(session, new StubEditor());

        session.SecurityFailure = new InvalidOperationException("VIEW DEFINITION 権限がありません。");
        await roles.ReloadAsync();

        Assert.Null(roles.Detail);
        var message = Assert.IsType<MessageNode>(Assert.Single(roles.Children));
        Assert.True(message.IsFailure);
    }

    /// <summary>行き先があることだけを表す差し込み。何を渡されたかを覚えておく。</summary>
    private sealed class StubEditor : IDatabaseRoleEditor
    {
        public bool Result { get; init; }

        public DatabaseName? CreatedFor { get; private set; }

        public DatabaseRoleDescriptor? EditedRole { get; private set; }

        public DatabaseRoleDescriptor? DeletedRole { get; private set; }

        public Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database)
        {
            CreatedFor = database;
            return Task.FromResult(Result);
        }

        public Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, DatabaseRoleDescriptor role)
        {
            EditedRole = role;
            return Task.FromResult(Result);
        }

        public Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, DatabaseRoleDescriptor role)
        {
            DeletedRole = role;
            return Task.FromResult(Result);
        }
    }

    private static async Task<DatabaseRolesNode> ExpandRolesAsync(
        FakeDatabaseSession session,
        IDatabaseRoleEditor? editor)
    {
        var explorer = NewExplorer(session, editor);
        await explorer.InitializeAsync();

        var database = Database(explorer);
        await database.EnsureChildrenAsync();

        var security = database.Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        var roles = security.Children.OfType<DatabaseRolesNode>().Single();
        await roles.EnsureChildrenAsync();

        return roles;
    }

    private static ObjectExplorerViewModel NewExplorer(FakeDatabaseSession session, IDatabaseRoleEditor? editor)
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
            Security = new DatabaseSecurityContext(new ListDatabaseUsersUseCase())
            {
                Roles = new ListDatabaseRolesUseCase(),
                RoleEditor = editor
            }
        };

        return new ObjectExplorerViewModel(context);
    }

    private static DatabaseNode Database(ObjectExplorerViewModel explorer) =>
        explorer.Roots[0].Children[0].Children.OfType<DatabaseNode>().First();

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession
        {
            Databases = [new DatabaseDescriptor(new DatabaseName("sales_db"))]
        }
        .WithDatabaseRoles("sales_db",
            new DatabaseRoleDescriptor(new RoleName("db_owner"), "dbo", IsFixedRole: true),
            new DatabaseRoleDescriptor(new RoleName("app_reader"), "dbo") { Members = ["app_user"] });
}
