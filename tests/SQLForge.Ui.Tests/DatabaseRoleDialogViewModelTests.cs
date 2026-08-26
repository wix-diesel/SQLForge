using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// データベース ロールのプロパティ ダイアログ。候補の並べ方、固定ロールで触らせない欄、
/// 保存で何がサーバーへ渡るかを確かめる。
/// </summary>
public class DatabaseRoleDialogViewModelTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public async Task 所有者とメンバーの候補はユーザーとロールから組む()
    {
        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb);
        await dialog.InitializeAsync();

        Assert.Equal(["app_reader", "app_user", "db_owner"], dialog.OwnerChoices);
        Assert.Equal(["app_reader", "app_user", "db_owner"], dialog.Members.Select(member => member.Name));
    }

    [Fact]
    public async Task 自分自身はメンバーの候補に出さない()
    {
        var original = new DatabaseRoleDescriptor(new RoleName("app_reader"));
        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb, original);
        await dialog.InitializeAsync();

        Assert.DoesNotContain("app_reader", dialog.Members.Select(member => member.Name));
    }

    [Fact]
    public async Task システムのスキーマは所有の候補に出さない()
    {
        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb);
        await dialog.InitializeAsync();

        Assert.Equal(["dbo", "sales"], dialog.Schemas.Select(schema => schema.Name));
    }

    [Fact]
    public async Task 今のメンバーと所有スキーマにはチェックが付く()
    {
        var original = new DatabaseRoleDescriptor(new RoleName("app_reader"), "dbo")
        {
            Members = ["app_user"],
            OwnedSchemas = ["sales"]
        };

        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb, original);
        await dialog.InitializeAsync();

        Assert.Equal(["app_user"], dialog.Members.Where(member => member.IsMember).Select(member => member.Name));
        Assert.Equal(["sales"], dialog.Schemas.Where(schema => schema.IsMember).Select(schema => schema.Name));
        Assert.Equal("dbo", dialog.Owner);
    }

    [Fact]
    public async Task 固定ロールでは名前と所有者を触らせない()
    {
        var original = new DatabaseRoleDescriptor(new RoleName("db_owner"), "dbo", IsFixedRole: true);
        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb, original);
        await dialog.InitializeAsync();

        Assert.False(dialog.CanEditDefinition);
    }

    [Fact]
    public async Task 保存すると作成としてサーバーへ渡り閉じる()
    {
        var session = NewSession();
        var dialog = SecurityDialogs.DatabaseRole(session, SalesDb);
        await dialog.InitializeAsync();

        var saved = false;
        dialog.CloseRequested += (_, result) => saved = result;

        dialog.Name = "reporting";
        dialog.Owner = "dbo";
        dialog.Members.First(member => member.Name == "app_user").IsMember = true;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.True(saved);
        Assert.Equal("reporting", session.CreatedDatabaseRole?.Name.Value);
        Assert.Equal(["app_user"], session.CreatedDatabaseRole?.Members);
    }

    [Fact]
    public async Task 名前が空なら閉じずに理由を出す()
    {
        var session = NewSession();
        var dialog = SecurityDialogs.DatabaseRole(session, SalesDb);
        await dialog.InitializeAsync();

        var closed = false;
        dialog.CloseRequested += (_, _) => closed = true;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.True(dialog.HasNameError);
        Assert.Equal("ロール名を入力してください。", dialog.ErrorMessage);
        Assert.Null(session.CreatedDatabaseRole);
    }

    [Fact]
    public async Task サーバーが弾いたら閉じずに理由を出す()
    {
        var session = NewSession();
        session.SecurityFailure = new InvalidOperationException("ALTER ANY ROLE 権限がありません。");

        var dialog = SecurityDialogs.DatabaseRole(session, SalesDb);
        await dialog.InitializeAsync();

        var closed = false;
        dialog.CloseRequested += (_, _) => closed = true;

        dialog.Name = "reporting";
        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal("ALTER ANY ROLE 権限がありません。", dialog.ErrorMessage);
    }

    [Fact]
    public async Task ページには全般と権限が並ぶ()
    {
        var dialog = SecurityDialogs.DatabaseRole(NewSession(), SalesDb);
        await dialog.InitializeAsync();

        Assert.Equal(["全般", "セキュリティ保護可能なリソース"], dialog.Pages.Select(page => page.Title));
        Assert.Same(dialog, dialog.SelectedPage.Content);
    }

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession()
            .WithSchemas("sales_db",
                new SchemaDescriptor(new SchemaName("dbo")),
                new SchemaDescriptor(new SchemaName("sales")),
                new SchemaDescriptor(new SchemaName("sys"), IsSystem: true))
            .WithDatabaseUsers("sales_db",
                new DatabaseUserDescriptor(new DatabaseUserName("app_user"), DatabaseUserType.SqlUserWithLogin, "app_login"))
            .WithDatabaseRoles("sales_db", "db_owner", "app_reader");
}
