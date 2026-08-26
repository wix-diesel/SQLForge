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
/// ツリーの「セキュリティ → サーバー ロール」の枝。SSMS と同じ並びで出ること、
/// 右クリックの追加・編集・削除がダイアログへつながり、済んだら読み直すこと。
/// </summary>
public class ServerRoleExplorerTests
{
    [Fact]
    public async Task サーバーのセキュリティの下にログインとロールの見出しが並ぶ()
    {
        var explorer = NewExplorer(NewSession(), new StubEditor());
        await explorer.InitializeAsync();

        var security = explorer.Roots[0].Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        Assert.Equal(["ログイン", "サーバー ロール"], security.Children.Select(node => node.Title));
    }

    [Fact]
    public async Task ロールは名前順に所有者とメンバー数を添えて並ぶ()
    {
        var roles = await ExpandRolesAsync(NewSession(), new StubEditor());

        Assert.Equal("2", roles.Detail);
        Assert.Equal(["deployers", "sysadmin"], roles.Children.Select(node => node.Title));

        var deployers = Assert.IsType<ServerRoleNode>(roles.Children[0]);
        Assert.Equal("所有者 sa · メンバー 1", deployers.Detail);
        Assert.False(deployers.IsSystem);
        Assert.True(roles.Children.OfType<ServerRoleNode>().Last().IsSystem);
    }

    [Fact]
    public async Task 固定ロールはプロパティを開けるが削除はさせない()
    {
        var roles = await ExpandRolesAsync(NewSession(), new StubEditor());
        var sysadmin = roles.Children.OfType<ServerRoleNode>().First(node => node.Title == "sysadmin");

        Assert.True(sysadmin.PropertiesCommand.CanExecute(null));
        Assert.False(sysadmin.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task 追加が済んだら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var roles = await ExpandRolesAsync(session, editor);
        var before = session.ServerRoleCallCount;

        await roles.NewRoleCommand.ExecuteAsync(null);

        Assert.True(editor.Created);
        Assert.Equal(before + 1, session.ServerRoleCallCount);
    }

    [Fact]
    public async Task 削除したら一覧を読み直す()
    {
        var session = NewSession();
        var editor = new StubEditor { Result = true };
        var roles = await ExpandRolesAsync(session, editor);
        var deployers = roles.Children.OfType<ServerRoleNode>().First(node => node.Title == "deployers");
        var before = session.ServerRoleCallCount;

        await deployers.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("deployers", editor.DeletedRole?.Name.Value);
        Assert.Equal(before + 1, session.ServerRoleCallCount);
    }

    /// <summary>行き先があることだけを表す差し込み。何を渡されたかを覚えておく。</summary>
    private sealed class StubEditor : IServerRoleEditor
    {
        public bool Result { get; init; }

        public bool Created { get; private set; }

        public ServerRoleDescriptor? EditedRole { get; private set; }

        public ServerRoleDescriptor? DeletedRole { get; private set; }

        public Task<bool> CreateAsync(IDatabaseSession session)
        {
            Created = true;
            return Task.FromResult(Result);
        }

        public Task<bool> EditAsync(IDatabaseSession session, ServerRoleDescriptor role)
        {
            EditedRole = role;
            return Task.FromResult(Result);
        }

        public Task<bool> DeleteAsync(IDatabaseSession session, ServerRoleDescriptor role)
        {
            DeletedRole = role;
            return Task.FromResult(Result);
        }
    }

    private static async Task<ServerRolesNode> ExpandRolesAsync(
        FakeDatabaseSession session,
        IServerRoleEditor? editor)
    {
        var explorer = NewExplorer(session, editor);
        await explorer.InitializeAsync();

        var security = explorer.Roots[0].Children.First(node => node.Title == "セキュリティ");
        await security.EnsureChildrenAsync();

        var roles = security.Children.OfType<ServerRolesNode>().Single();
        await roles.EnsureChildrenAsync();

        return roles;
    }

    private static ObjectExplorerViewModel NewExplorer(FakeDatabaseSession session, IServerRoleEditor? editor)
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
            ServerSecurity = new ServerSecurityContext(new ListServerLoginsUseCase())
            {
                Roles = new ListServerRolesUseCase(),
                RoleEditor = editor
            }
        };

        return new ObjectExplorerViewModel(context);
    }

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession().WithServerRoles(
            new ServerRoleDescriptor(new RoleName("sysadmin"), "sa", IsFixedRole: true),
            new ServerRoleDescriptor(new RoleName("deployers"), "sa") { Members = ["app_login"] });
}
