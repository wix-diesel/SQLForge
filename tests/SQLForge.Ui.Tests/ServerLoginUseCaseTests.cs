using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// サーバー ログインの一覧・追加・編集・削除のユースケース。
/// 並べ方と、サーバーへ何を渡すかをここで固定する。
/// </summary>
public class ServerLoginUseCaseTests
{
    [Fact]
    public async Task ログインは利用者が作ったものが先で名前順に並ぶ()
    {
        var session = new FakeDatabaseSession().WithServerLogins(
            Login("sa", isSystem: true),
            Login("reporting_login"),
            Login("app_login"),
            Login("##MS_PolicyEventProcessingLogin##", isSystem: true));

        var logins = await new ListServerLoginsUseCase().ExecuteAsync(session);

        Assert.Equal(
            ["app_login", "reporting_login", "##MS_PolicyEventProcessingLogin##", "sa"],
            logins.Select(login => login.Name.Value));
    }

    [Fact]
    public async Task サーバーロールは名前順に並ぶ()
    {
        var session = new FakeDatabaseSession().WithServerRoles("sysadmin", "dbcreator", "processadmin");

        var roles = await new ListServerRolesUseCase().ExecuteAsync(session);

        Assert.Equal(["dbcreator", "processadmin", "sysadmin"], roles.Select(role => role.Name.Value));
    }

    [Fact]
    public async Task 新しいログインは作成としてサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();
        var draft = Draft() with { Roles = ["dbcreator"] };

        var result = await new SaveServerLoginUseCase().ExecuteAsync(session, draft);

        Assert.True(result.IsValid);
        Assert.Equal("app_login", session.CreatedLogin?.Name.Value);
        Assert.Equal("p@ssw0rd", session.CreatedLogin?.Password);
        Assert.Equal("sales_db", session.CreatedLogin?.DefaultDatabase?.Value);
        Assert.Equal(["dbcreator"], session.CreatedLogin?.Roles);
        Assert.Null(session.AlteredLogin);
    }

    [Fact]
    public async Task 既存のログインは変更として元の姿と一緒にサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();
        var original = Login("app_login");
        var draft = ServerLoginDraft.FromDescriptor(original) with { DefaultDatabase = "audit_db" };

        var result = await new SaveServerLoginUseCase().ExecuteAsync(session, draft);

        Assert.True(result.IsValid);
        Assert.Null(session.CreatedLogin);
        Assert.Same(original, session.AlteredOriginalLogin);
        Assert.Equal("audit_db", session.AlteredLogin?.DefaultDatabase?.Value);

        // パスワード欄は空のまま。変えないものを文面へ持ち出さない。
        Assert.Null(session.AlteredLogin?.Password);
    }

    [Fact]
    public async Task 入力が妥当でなければサーバーへは渡さない()
    {
        var session = new FakeDatabaseSession();

        var result = await new SaveServerLoginUseCase().ExecuteAsync(session, Draft(name: string.Empty));

        Assert.False(result.IsValid);
        Assert.Equal("ログイン名を入力してください。", result.FirstError);
        Assert.Null(session.CreatedLogin);
        Assert.Null(session.AlteredLogin);
    }

    [Fact]
    public async Task ログインを削除するとサーバーへ名前が渡る()
    {
        var session = new FakeDatabaseSession();

        await new DropServerLoginUseCase().ExecuteAsync(session, Login("app_login"));

        Assert.Equal("app_login", session.DroppedLogin?.Value);
    }

    [Fact]
    public async Task システムのログインは削除させない()
    {
        var session = new FakeDatabaseSession();

        var rejected = await Assert.ThrowsAsync<ServerLoginRejectedException>(
            () => new DropServerLoginUseCase().ExecuteAsync(session, Login("sa", isSystem: true)));

        Assert.Equal("システムのログインは削除できません。", rejected.Message);
        Assert.Null(session.DroppedLogin);
    }

    private static ServerLoginDraft Draft(string name = "app_login") =>
        new()
        {
            Name = name,
            Type = ServerLoginType.SqlLogin,
            Password = "p@ssw0rd",
            PasswordConfirmation = "p@ssw0rd",
            EnforcePolicy = true,
            EnforceExpiration = true,
            MustChangePassword = false,
            DefaultDatabase = "sales_db"
        };

    private static ServerLoginDescriptor Login(string name, bool isSystem = false) =>
        new(new ServerLoginName(name),
            ServerLoginType.SqlLogin,
            new DatabaseName("master"),
            IsDisabled: false,
            IsSystem: isSystem)
        {
            PasswordPolicy = ServerLoginPasswordPolicy.Default
        };
}
