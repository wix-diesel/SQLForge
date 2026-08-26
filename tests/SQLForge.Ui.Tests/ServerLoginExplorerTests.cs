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
/// ツリーのサーバー直下「セキュリティ → ログイン」の枝。SSMS と同じ並びで出ること、
/// 右クリックの追加・編集・削除がダイアログへつながり、済んだら読み直すこと。
/// </summary>
public class ServerLoginExplorerTests
{
    [Fact]
    public async Task サーバーの下にデータベースとセキュリティが並ぶ()
    {
        var explorer = NewExplorer(NewSession(), new StubEditor());
        await explorer.InitializeAsync();

        var server = explorer.Roots[0];
        Assert.Equal(["データベース", "セキュリティ"], server.Children.Select(node => node.Title));

        var security = server.Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        // 中身が固定の見出しなので「1」とは出さない。
        Assert.Null(security.Detail);

        var logins = Assert.IsType<ServerLoginsNode>(Assert.Single(security.Children));
        Assert.Equal("ログイン", logins.Title);
    }

    [Fact]
    public async Task 一式が無ければ枝そのものを出さない()
    {
        // ツリーだけを組む構成（カタログしか読まないとき）にログインの読み取り権限まで要求しない。
        var explorer = NewExplorer(NewSession(), editor: null, withSecurity: false);
        await explorer.InitializeAsync();

        Assert.Equal(["データベース"], explorer.Roots[0].Children.Select(node => node.Title));
    }

    [Fact]
    public async Task ログインは利用者が作ったものが先で認証方式を添えて並ぶ()
    {
        var logins = await ExpandLoginsAsync(NewSession(), new StubEditor());

        Assert.Equal("3", logins.Detail);
        Assert.Equal(["app_login", "reporting_login", "sa"], logins.Children.Select(node => node.Title));

        var appLogin = Assert.IsType<ServerLoginNode>(logins.Children[0]);
        Assert.Equal("SQL Server 認証のログイン · sales_db", appLogin.Detail);
        Assert.False(appLogin.IsSystem);

        // 無効なログインは繋げないので、開かなくても分かるようにしておく。
        var reporting = Assert.IsType<ServerLoginNode>(logins.Children[1]);
        Assert.Equal("Windows 認証のログイン · 無効", reporting.Detail);
    }

    [Fact]
    public async Task システムのログインは編集も削除もさせない()
    {
        var logins = await ExpandLoginsAsync(NewSession(), new StubEditor());
        var sa = logins.Children.OfType<ServerLoginNode>().First(node => node.Title == "sa");

        Assert.True(sa.IsSystem);
        Assert.False(sa.PropertiesCommand.CanExecute(null));
        Assert.False(sa.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task 行き先がつながっていなければ追加も編集も押せない()
    {
        var logins = await ExpandLoginsAsync(NewSession(), editor: null);

        Assert.False(logins.NewLoginCommand.CanExecute(null));
        Assert.False(logins.Children.OfType<ServerLoginNode>().First().PropertiesCommand.CanExecute(null));
    }

    [Fact]
    public async Task 追加が済んだら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var logins = await ExpandLoginsAsync(session, editor);
        var before = session.ServerLoginCallCount;

        await logins.NewLoginCommand.ExecuteAsync(null);

        Assert.True(editor.Created);
        Assert.Equal(before + 1, session.ServerLoginCallCount);
    }

    [Fact]
    public async Task やめたときは読み直さない()
    {
        var session = NewSession();
        var logins = await ExpandLoginsAsync(session, new StubEditor { Result = false });
        var before = session.ServerLoginCallCount;

        await logins.NewLoginCommand.ExecuteAsync(null);

        Assert.Equal(before, session.ServerLoginCallCount);
    }

    [Fact]
    public async Task プロパティで変えたら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var logins = await ExpandLoginsAsync(session, editor);
        var appLogin = logins.Children.OfType<ServerLoginNode>().First(node => node.Title == "app_login");
        var before = session.ServerLoginCallCount;

        await appLogin.PropertiesCommand.ExecuteAsync(null);

        Assert.Equal("app_login", editor.EditedLogin?.Name.Value);
        Assert.Equal(before + 1, session.ServerLoginCallCount);
    }

    [Fact]
    public async Task 削除したら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var logins = await ExpandLoginsAsync(session, editor);
        var appLogin = logins.Children.OfType<ServerLoginNode>().First(node => node.Title == "app_login");
        var before = session.ServerLoginCallCount;

        await appLogin.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("app_login", editor.DeletedLogin?.Name.Value);
        Assert.Equal(before + 1, session.ServerLoginCallCount);
    }

    [Fact]
    public async Task 読み込みに失敗したら理由を子の行に出して件数を消す()
    {
        var session = NewSession();
        var logins = await ExpandLoginsAsync(session, new StubEditor());

        session.SecurityFailure = new InvalidOperationException("VIEW ANY DEFINITION 権限がありません。");
        await logins.ReloadAsync();

        Assert.Null(logins.Detail);
        var message = Assert.IsType<MessageNode>(Assert.Single(logins.Children));
        Assert.True(message.IsFailure);
        Assert.Equal("VIEW ANY DEFINITION 権限がありません。", message.Title);
    }

    /// <summary>行き先があることだけを表す差し込み。何を渡されたかを覚えておく。</summary>
    private sealed class StubEditor : IServerLoginEditor
    {
        public bool Result { get; init; }

        public bool Created { get; private set; }

        public ServerLoginDescriptor? EditedLogin { get; private set; }

        public ServerLoginDescriptor? DeletedLogin { get; private set; }

        public Task<bool> CreateAsync(IDatabaseSession session)
        {
            Created = true;
            return Task.FromResult(Result);
        }

        public Task<bool> EditAsync(IDatabaseSession session, ServerLoginDescriptor login)
        {
            EditedLogin = login;
            return Task.FromResult(Result);
        }

        public Task<bool> DeleteAsync(IDatabaseSession session, ServerLoginDescriptor login)
        {
            DeletedLogin = login;
            return Task.FromResult(Result);
        }
    }

    private static async Task<ServerLoginsNode> ExpandLoginsAsync(
        FakeDatabaseSession session,
        IServerLoginEditor? editor)
    {
        var explorer = NewExplorer(session, editor);
        await explorer.InitializeAsync();

        var security = explorer.Roots[0].Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        var logins = (ServerLoginsNode)security.Children.Single();
        await logins.EnsureChildrenAsync();

        return logins;
    }

    private static ObjectExplorerViewModel NewExplorer(
        FakeDatabaseSession session,
        IServerLoginEditor? editor,
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
            ServerSecurity = withSecurity ? new ServerSecurityContext(new ListServerLoginsUseCase(), editor) : null
        };

        return new ObjectExplorerViewModel(context);
    }

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession
        {
            Databases = [new DatabaseDescriptor(new DatabaseName("sales_db"))]
        }
        .WithServerLogins(
            new ServerLoginDescriptor(
                new ServerLoginName("sa"),
                ServerLoginType.SqlLogin,
                new DatabaseName("master"),
                IsSystem: true),
            new ServerLoginDescriptor(
                new ServerLoginName("reporting_login"),
                ServerLoginType.WindowsUser,
                IsDisabled: true),
            new ServerLoginDescriptor(
                new ServerLoginName("app_login"),
                ServerLoginType.SqlLogin,
                new DatabaseName("sales_db")));
}
