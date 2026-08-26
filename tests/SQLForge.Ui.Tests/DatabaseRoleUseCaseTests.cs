using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// データベース ロールの一覧・追加・編集・削除のユースケース。
/// 並べ方と、サーバーへ何を渡すかをここで固定する。
/// </summary>
public class DatabaseRoleUseCaseTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public async Task ロールは固定ロールも混ぜて名前順に並ぶ()
    {
        // SSMS の一覧も固定ロールを分けずに名前順で出す。
        var session = new FakeDatabaseSession().WithDatabaseRoles(
            "sales_db",
            new DatabaseRoleDescriptor(new RoleName("db_owner"), IsFixedRole: true),
            new DatabaseRoleDescriptor(new RoleName("app_reader")),
            new DatabaseRoleDescriptor(new RoleName("db_datareader"), IsFixedRole: true));

        var roles = await new ListDatabaseRolesUseCase().ExecuteAsync(session, SalesDb);

        Assert.Equal(["app_reader", "db_datareader", "db_owner"], roles.Select(role => role.Name.Value));
    }

    [Fact]
    public async Task 新しいロールは作成としてサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();

        var result = await new SaveDatabaseRoleUseCase().ExecuteAsync(
            session,
            SalesDb,
            new DatabaseRoleDraft
            {
                Name = " app_reader ",
                Owner = " dbo ",
                Members = ["app_user"],
                OwnedSchemas = ["sales"]
            });

        Assert.True(result.IsValid);
        Assert.Null(session.AlteredDatabaseRole);

        var created = Assert.IsType<DatabaseRoleDefinition>(session.CreatedDatabaseRole);
        Assert.Equal("app_reader", created.Name.Value);
        Assert.Equal("dbo", created.Owner);
        Assert.Equal(["app_user"], created.Members);
        Assert.Equal(["sales"], created.OwnedSchemas);
    }

    [Fact]
    public async Task 元の姿を持つ下書きは変更としてサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();
        var original = new DatabaseRoleDescriptor(new RoleName("app_reader"), "dbo");

        var result = await new SaveDatabaseRoleUseCase().ExecuteAsync(
            session,
            SalesDb,
            DatabaseRoleDraft.FromDescriptor(original) with { Name = "reporting" });

        Assert.True(result.IsValid);
        Assert.Null(session.CreatedDatabaseRole);
        Assert.Equal(original, session.AlteredOriginalDatabaseRole);
        Assert.Equal("reporting", session.AlteredDatabaseRole?.Name.Value);
    }

    [Fact]
    public async Task 名前が空なら送らずに理由を返す()
    {
        var session = new FakeDatabaseSession();

        var result = await new SaveDatabaseRoleUseCase().ExecuteAsync(
            session,
            SalesDb,
            new DatabaseRoleDraft { Name = "  ", Owner = string.Empty });

        Assert.False(result.IsValid);
        Assert.Equal("ロール名を入力してください。", result[DatabaseRoleValidator.NameField]);
        Assert.Null(session.CreatedDatabaseRole);
    }

    [Fact]
    public async Task 固定ロールの名前と所有者は変えられない()
    {
        var session = new FakeDatabaseSession();
        var original = new DatabaseRoleDescriptor(new RoleName("db_owner"), "dbo", IsFixedRole: true);

        var result = await new SaveDatabaseRoleUseCase().ExecuteAsync(
            session,
            SalesDb,
            DatabaseRoleDraft.FromDescriptor(original) with { Name = "owners", Owner = "app_user" });

        Assert.False(result.IsValid);
        Assert.Equal("固定のデータベース ロールは名前を変更できません。", result[DatabaseRoleValidator.NameField]);
        Assert.Equal("固定のデータベース ロールは所有者を変更できません。", result[DatabaseRoleValidator.OwnerField]);
        Assert.Null(session.AlteredDatabaseRole);
    }

    [Fact]
    public async Task 固定ロールでもメンバーの出し入れは通る()
    {
        var session = new FakeDatabaseSession();
        var original = new DatabaseRoleDescriptor(new RoleName("db_datareader"), IsFixedRole: true);

        var result = await new SaveDatabaseRoleUseCase().ExecuteAsync(
            session,
            SalesDb,
            DatabaseRoleDraft.FromDescriptor(original) with { Members = ["app_user"] });

        Assert.True(result.IsValid);
        Assert.Equal(["app_user"], session.AlteredDatabaseRole?.Members);
    }

    [Fact]
    public async Task ロールの削除はそのまま渡る()
    {
        var session = new FakeDatabaseSession();

        await new DropDatabaseRoleUseCase().ExecuteAsync(
            session,
            SalesDb,
            new DatabaseRoleDescriptor(new RoleName("app_reader")));

        Assert.Equal("app_reader", session.DroppedDatabaseRole?.Value);
    }

    [Fact]
    public async Task 固定ロールは削除させない()
    {
        var session = new FakeDatabaseSession();

        await Assert.ThrowsAsync<DatabaseRoleRejectedException>(() =>
            new DropDatabaseRoleUseCase().ExecuteAsync(
                session,
                SalesDb,
                new DatabaseRoleDescriptor(new RoleName("db_owner"), IsFixedRole: true)));

        Assert.Null(session.DroppedDatabaseRole);
    }
}
