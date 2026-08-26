using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// スキーマのプロパティ ダイアログ。所有者の候補と、名前を触らせない編集を確かめる。
/// </summary>
public class SchemaDialogViewModelTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public async Task 所有者の候補はユーザーとロールから組む()
    {
        var dialog = SecurityDialogs.Schema(NewSession(), SalesDb);
        await dialog.InitializeAsync();

        Assert.Equal(["app_reader", "app_user", "db_owner"], dialog.OwnerChoices);
        Assert.True(dialog.CanChangeName);
    }

    [Fact]
    public async Task 既存のスキーマでは名前を触らせない()
    {
        var dialog = SecurityDialogs.Schema(
            NewSession(),
            SalesDb,
            new SchemaDescriptor(new SchemaName("sales"), Owner: "dbo"));

        await dialog.InitializeAsync();

        Assert.False(dialog.CanChangeName);
        Assert.Equal("sales", dialog.Name);
        Assert.Equal("dbo", dialog.Owner);
        Assert.Equal("スキーマ — sales", dialog.Title);
    }

    [Fact]
    public async Task 保存すると作成としてサーバーへ渡り閉じる()
    {
        var session = NewSession();
        var dialog = SecurityDialogs.Schema(session, SalesDb);
        await dialog.InitializeAsync();

        var saved = false;
        dialog.CloseRequested += (_, result) => saved = result;

        dialog.Name = "staging";
        dialog.Owner = "app_user";

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.True(saved);
        Assert.Equal("staging", session.CreatedSchema?.Name.Value);
        Assert.Equal("app_user", session.CreatedSchema?.Owner);
    }

    [Fact]
    public async Task 名前が空なら閉じずに理由を出す()
    {
        var session = NewSession();
        var dialog = SecurityDialogs.Schema(session, SalesDb);
        await dialog.InitializeAsync();

        var closed = false;
        dialog.CloseRequested += (_, _) => closed = true;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.True(dialog.HasNameError);
        Assert.Null(session.CreatedSchema);
    }

    [Fact]
    public async Task やめたときは何も渡さずに閉じる()
    {
        var session = NewSession();
        var dialog = SecurityDialogs.Schema(session, SalesDb);
        await dialog.InitializeAsync();

        var saved = true;
        dialog.CloseRequested += (_, result) => saved = result;

        dialog.CancelCommand.Execute(null);

        Assert.False(saved);
        Assert.Null(session.CreatedSchema);
    }

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession()
            .WithDatabaseUsers("sales_db",
                new DatabaseUserDescriptor(
                    new DatabaseUserName("app_user"),
                    DatabaseUserType.SqlUserWithLogin,
                    "app_login"))
            .WithDatabaseRoles("sales_db", "db_owner", "app_reader");
}
