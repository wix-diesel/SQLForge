using SQLForge.Application.Security;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// サーバー ロールの一覧・追加・編集・削除のユースケース。
/// 並べ方と、サーバーへ何を渡すかをここで固定する。
/// </summary>
public class ServerRoleUseCaseTests
{
    [Fact]
    public async Task ロールは固定ロールも混ぜて名前順に並ぶ()
    {
        var session = new FakeDatabaseSession().WithServerRoles(
            new ServerRoleDescriptor(new RoleName("sysadmin"), IsFixedRole: true),
            new ServerRoleDescriptor(new RoleName("deployers")),
            new ServerRoleDescriptor(new RoleName("dbcreator"), IsFixedRole: true));

        var roles = await new ListServerRolesUseCase().ExecuteAsync(session);

        Assert.Equal(["dbcreator", "deployers", "sysadmin"], roles.Select(role => role.Name.Value));
    }

    [Fact]
    public async Task 新しいロールは作成としてサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();

        var result = await new SaveServerRoleUseCase().ExecuteAsync(
            session,
            new ServerRoleDraft
            {
                Name = " deployers ",
                Owner = " sa ",
                Members = ["app_login"],
                Memberships = ["dbcreator"]
            });

        Assert.True(result.IsValid);

        var created = Assert.IsType<ServerRoleDefinition>(session.CreatedServerRole);
        Assert.Equal("deployers", created.Name.Value);
        Assert.Equal("sa", created.Owner);
        Assert.Equal(["app_login"], created.Members);
        Assert.Equal(["dbcreator"], created.Memberships);
    }

    [Fact]
    public async Task 元の姿を持つ下書きは変更としてサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();
        var original = new ServerRoleDescriptor(new RoleName("deployers"), "sa");

        var result = await new SaveServerRoleUseCase().ExecuteAsync(
            session,
            ServerRoleDraft.FromDescriptor(original) with { Members = ["app_login"] });

        Assert.True(result.IsValid);
        Assert.Null(session.CreatedServerRole);
        Assert.Equal(original, session.AlteredOriginalServerRole);
        Assert.Equal(["app_login"], session.AlteredServerRole?.Members);
    }

    [Fact]
    public async Task 固定ロールの名前とメンバーシップは変えられない()
    {
        var session = new FakeDatabaseSession();
        var original = new ServerRoleDescriptor(new RoleName("sysadmin"), IsFixedRole: true);

        var result = await new SaveServerRoleUseCase().ExecuteAsync(
            session,
            ServerRoleDraft.FromDescriptor(original) with
            {
                Name = "admins",
                Memberships = ["dbcreator"]
            });

        Assert.False(result.IsValid);
        Assert.Equal("固定のサーバー ロールは名前を変更できません。", result[ServerRoleValidator.NameField]);
        Assert.Equal(
            "固定のサーバー ロールのメンバーシップは変更できません。",
            result[ServerRoleValidator.MembershipField]);
        Assert.Null(session.AlteredServerRole);
    }

    [Fact]
    public async Task 固定ロールは削除させない()
    {
        var session = new FakeDatabaseSession();

        await Assert.ThrowsAsync<ServerRoleRejectedException>(() =>
            new DropServerRoleUseCase().ExecuteAsync(
                session,
                new ServerRoleDescriptor(new RoleName("sysadmin"), IsFixedRole: true)));

        Assert.Null(session.DroppedServerRole);
    }

    [Fact]
    public async Task ロールの削除はそのまま渡る()
    {
        var session = new FakeDatabaseSession();

        await new DropServerRoleUseCase().ExecuteAsync(session, new ServerRoleDescriptor(new RoleName("deployers")));

        Assert.Equal("deployers", session.DroppedServerRole?.Value);
    }
}
