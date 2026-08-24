using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// データベース ユーザーの一覧・追加・編集・削除のユースケース。
/// 並べ方と、サーバーへ何を渡すかをここで固定する。
/// </summary>
public class DatabaseUserUseCaseTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public async Task ユーザーは利用者が作ったものが先で名前順に並ぶ()
    {
        var session = new FakeDatabaseSession().WithDatabaseUsers(
            "sales_db",
            User("dbo", isSystem: true),
            User("reporting"),
            User("app_user"),
            User("guest", isSystem: true));

        var users = await new ListDatabaseUsersUseCase().ExecuteAsync(session, SalesDb);

        Assert.Equal(["app_user", "reporting", "dbo", "guest"], users.Select(user => user.Name.Value));
    }

    [Fact]
    public async Task ロールは名前順に並ぶ()
    {
        var session = new FakeDatabaseSession()
            .WithDatabaseRoles("sales_db", "db_owner", "app_reader", "db_datareader");

        var roles = await new ListDatabaseRolesUseCase().ExecuteAsync(session, SalesDb);

        Assert.Equal(["app_reader", "db_datareader", "db_owner"], roles);
    }

    [Fact]
    public async Task 新しいユーザーは作成としてサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();
        var draft = new DatabaseUserDraft
        {
            Name = "app_user",
            Type = DatabaseUserType.SqlUserWithLogin,
            LoginName = "app_login",
            DefaultSchema = "sales",
            Roles = ["db_datareader"]
        };

        var result = await new SaveDatabaseUserUseCase().ExecuteAsync(session, SalesDb, draft);

        Assert.True(result.IsValid);
        Assert.Equal("sales_db", session.CreatedUserDatabase);
        Assert.Equal("app_user", session.CreatedUser?.Name.Value);
        Assert.Equal("app_login", session.CreatedUser?.LoginName);
        Assert.Equal(["db_datareader"], session.CreatedUser?.Roles);
        Assert.Null(session.AlteredUser);
    }

    [Fact]
    public async Task 既存のユーザーは変更として元の姿と一緒にサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();
        var original = User("app_user") with { LoginName = "old_login" };
        var draft = DatabaseUserDraft.FromDescriptor(original) with { LoginName = "new_login" };

        var result = await new SaveDatabaseUserUseCase().ExecuteAsync(session, SalesDb, draft);

        Assert.True(result.IsValid);
        Assert.Null(session.CreatedUser);
        Assert.Same(original, session.AlteredOriginal);
        Assert.Equal("new_login", session.AlteredUser?.LoginName);
    }

    [Fact]
    public async Task 入力が妥当でなければサーバーへは渡さない()
    {
        var session = new FakeDatabaseSession();
        var draft = new DatabaseUserDraft
        {
            Name = string.Empty,
            Type = DatabaseUserType.SqlUserWithLogin,
            LoginName = "app_login",
            DefaultSchema = string.Empty
        };

        var result = await new SaveDatabaseUserUseCase().ExecuteAsync(session, SalesDb, draft);

        Assert.False(result.IsValid);
        Assert.Equal("ユーザー名を入力してください。", result.FirstError);
        Assert.Null(session.CreatedUser);
        Assert.Null(session.AlteredUser);
    }

    [Fact]
    public async Task ユーザーを削除するとサーバーへ名前が渡る()
    {
        var session = new FakeDatabaseSession();

        await new DropDatabaseUserUseCase().ExecuteAsync(session, SalesDb, User("app_user"));

        Assert.Equal("sales_db", session.DroppedUserDatabase);
        Assert.Equal("app_user", session.DroppedUser?.Value);
    }

    [Fact]
    public async Task システムのユーザーは削除させない()
    {
        var session = new FakeDatabaseSession();

        var rejected = await Assert.ThrowsAsync<DatabaseUserRejectedException>(
            () => new DropDatabaseUserUseCase().ExecuteAsync(session, SalesDb, User("dbo", isSystem: true)));

        Assert.Equal("システムのユーザーは削除できません。", rejected.Message);
        Assert.Null(session.DroppedUser);
    }

    private static DatabaseUserDescriptor User(string name, bool isSystem = false) =>
        new(new DatabaseUserName(name),
            DatabaseUserType.SqlUserWithLogin,
            LoginName: name,
            DefaultSchema: new SchemaName("dbo"),
            IsSystem: isSystem);
}
