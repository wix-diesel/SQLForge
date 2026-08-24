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
/// ツリーの「セキュリティ → ユーザー」の枝。SSMS と同じ並びで出ること、
/// 右クリックの追加・編集・削除がダイアログへつながり、済んだら読み直すこと。
/// </summary>
public class DatabaseUserExplorerTests
{
    [Fact]
    public async Task セキュリティの下にユーザーの見出しが並ぶ()
    {
        var explorer = NewExplorer(NewSession(), new StubEditor());
        await explorer.InitializeAsync();

        var database = Database(explorer);
        await database.EnsureChildrenAsync();

        Assert.Equal(["スキーマ", "セキュリティ"], database.Children.Select(node => node.Title));

        var security = database.Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        // 中身が固定の見出しなので「1」とは出さない。
        Assert.Null(security.Detail);

        var users = Assert.IsType<DatabaseUsersNode>(Assert.Single(security.Children));
        Assert.Equal("ユーザー", users.Title);
    }

    [Fact]
    public async Task セキュリティの一式が無ければ枝そのものを出さない()
    {
        // ツリーだけを組む構成（カタログしか読まないとき）にユーザーの権限まで要求しない。
        var explorer = NewExplorer(NewSession(), editor: null, withSecurity: false);
        await explorer.InitializeAsync();

        var database = Database(explorer);
        await database.EnsureChildrenAsync();

        Assert.Equal(["スキーマ"], database.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task ユーザーは利用者が作ったものが先で種類とログインを添えて並ぶ()
    {
        var users = await ExpandUsersAsync(NewSession(), new StubEditor());

        Assert.Equal("3", users.Detail);
        Assert.Equal(["app_user", "reporting", "dbo"], users.Children.Select(node => node.Title));

        var appUser = Assert.IsType<DatabaseUserNode>(users.Children[0]);
        Assert.Equal("SQL ユーザー（ログインあり） · app_login", appUser.Detail);
        Assert.False(appUser.IsSystem);

        var reporting = Assert.IsType<DatabaseUserNode>(users.Children[1]);
        Assert.Equal("SQL ユーザー（ログインなし）", reporting.Detail);
    }

    [Fact]
    public async Task システムのユーザーは編集も削除もさせない()
    {
        var users = await ExpandUsersAsync(NewSession(), new StubEditor());
        var dbo = users.Children.OfType<DatabaseUserNode>().First(node => node.Title == "dbo");

        Assert.True(dbo.IsSystem);
        Assert.False(dbo.PropertiesCommand.CanExecute(null));
        Assert.False(dbo.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task 行き先がつながっていなければ追加も編集も押せない()
    {
        var users = await ExpandUsersAsync(NewSession(), editor: null);

        Assert.False(users.NewUserCommand.CanExecute(null));
        Assert.False(users.Children.OfType<DatabaseUserNode>().First().PropertiesCommand.CanExecute(null));
    }

    [Fact]
    public async Task 追加が済んだら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var users = await ExpandUsersAsync(session, editor);
        var before = session.DatabaseUserCallCount;

        await users.NewUserCommand.ExecuteAsync(null);

        Assert.Equal("sales_db", editor.CreatedFor?.Value);
        Assert.Equal(before + 1, session.DatabaseUserCallCount);
    }

    [Fact]
    public async Task やめたときは読み直さない()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = false };
        var users = await ExpandUsersAsync(session, editor);
        var before = session.DatabaseUserCallCount;

        await users.NewUserCommand.ExecuteAsync(null);

        Assert.Equal(before, session.DatabaseUserCallCount);
    }

    [Fact]
    public async Task プロパティで変えたら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var users = await ExpandUsersAsync(session, editor);
        var appUser = users.Children.OfType<DatabaseUserNode>().First(node => node.Title == "app_user");
        var before = session.DatabaseUserCallCount;

        await appUser.PropertiesCommand.ExecuteAsync(null);

        Assert.Equal("app_user", editor.EditedUser?.Name.Value);
        Assert.Equal(before + 1, session.DatabaseUserCallCount);
    }

    [Fact]
    public async Task 削除したら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var users = await ExpandUsersAsync(session, editor);
        var appUser = users.Children.OfType<DatabaseUserNode>().First(node => node.Title == "app_user");
        var before = session.DatabaseUserCallCount;

        await appUser.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("app_user", editor.DeletedUser?.Name.Value);
        Assert.Equal(before + 1, session.DatabaseUserCallCount);
    }

    [Fact]
    public async Task 読み込みに失敗したら理由を子の行に出して件数を消す()
    {
        var session = NewSession();
        var users = await ExpandUsersAsync(session, new StubEditor());

        session.SecurityFailure = new InvalidOperationException("VIEW DEFINITION 権限がありません。");
        await users.ReloadAsync();

        Assert.Null(users.Detail);
        var message = Assert.IsType<MessageNode>(Assert.Single(users.Children));
        Assert.True(message.IsFailure);
        Assert.Equal("VIEW DEFINITION 権限がありません。", message.Title);
    }

    /// <summary>行き先があることだけを表す差し込み。何を渡されたかを覚えておく。</summary>
    private sealed class StubEditor : IDatabaseUserEditor
    {
        public bool Result { get; init; }

        public DatabaseName? CreatedFor { get; private set; }

        public DatabaseUserDescriptor? EditedUser { get; private set; }

        public DatabaseUserDescriptor? DeletedUser { get; private set; }

        public Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database)
        {
            CreatedFor = database;
            return Task.FromResult(Result);
        }

        public Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, DatabaseUserDescriptor user)
        {
            EditedUser = user;
            return Task.FromResult(Result);
        }

        public Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, DatabaseUserDescriptor user)
        {
            DeletedUser = user;
            return Task.FromResult(Result);
        }
    }

    private static async Task<DatabaseUsersNode> ExpandUsersAsync(
        FakeDatabaseSession session,
        IDatabaseUserEditor? editor)
    {
        var explorer = NewExplorer(session, editor);
        await explorer.InitializeAsync();

        var database = Database(explorer);
        await database.EnsureChildrenAsync();

        var security = database.Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        var users = (DatabaseUsersNode)security.Children.Single();
        await users.EnsureChildrenAsync();

        return users;
    }

    private static ObjectExplorerViewModel NewExplorer(
        FakeDatabaseSession session,
        IDatabaseUserEditor? editor,
        bool withSecurity = true)
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
            Security = withSecurity ? new DatabaseSecurityContext(new ListDatabaseUsersUseCase(), editor) : null
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
        .WithDatabaseUsers("sales_db",
            new DatabaseUserDescriptor(
                new DatabaseUserName("dbo"),
                DatabaseUserType.SqlUserWithLogin,
                "sa",
                new SchemaName("dbo"),
                IsSystem: true),
            new DatabaseUserDescriptor(
                new DatabaseUserName("reporting"),
                DatabaseUserType.SqlUserWithoutLogin),
            new DatabaseUserDescriptor(
                new DatabaseUserName("app_user"),
                DatabaseUserType.SqlUserWithLogin,
                "app_login",
                new SchemaName("sales")));
}
