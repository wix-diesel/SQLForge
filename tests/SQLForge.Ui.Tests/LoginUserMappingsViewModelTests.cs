using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 「ユーザー マッピング」のページ。データベースが並ぶこと、対応づけ済みの行に
/// チェックが付くこと、チェックを付けたらログイン名が初期値になること。
/// </summary>
public class LoginUserMappingsViewModelTests
{
    [Fact]
    public async Task アクセスできるデータベースが並ぶ()
    {
        var page = NewPage(NewSession(), "app_login", isNew: false);
        await page.InitializeAsync();

        // 開けないデータベースは中を見られないので出さない。
        Assert.Equal(["sales_db", "master"], page.Rows.Select(row => row.Database));
    }

    [Fact]
    public async Task 対応づけ済みの行にはチェックと今の値が入る()
    {
        var page = NewPage(NewSession(), "app_login", isNew: false);
        await page.InitializeAsync();

        var sales = page.Rows.First(row => row.Database == "sales_db");

        Assert.True(sales.IsMapped);
        Assert.Equal("app_user", sales.UserName);
        Assert.Equal("sales", sales.DefaultSchema);
        Assert.Same(sales, page.SelectedRow);

        var master = page.Rows.First(row => row.Database == "master");
        Assert.False(master.IsMapped);
        Assert.Equal(string.Empty, master.UserName);
    }

    [Fact]
    public async Task チェックを付けたらログイン名が初期値になる()
    {
        var page = NewPage(NewSession(), "app_login", isNew: false);
        await page.InitializeAsync();

        var master = page.Rows.First(row => row.Database == "master");
        master.IsMapped = true;

        Assert.Equal("app_login", master.UserName);
    }

    [Fact]
    public async Task 選んだ行のロールだけを読む()
    {
        var session = NewSession();
        var page = NewPage(session, "app_login", isNew: false);
        await page.InitializeAsync();

        var sales = page.Rows.First(row => row.Database == "sales_db");

        Assert.Equal(["app_reader", "db_datareader"], sales.Roles.Select(role => role.Name));
        Assert.Equal(["db_datareader"], sales.Roles.Where(role => role.IsMember).Select(role => role.Name));

        // 選ばれていない行はまだ読んでいない。
        Assert.Empty(page.Rows.First(row => row.Database == "master").Roles);
    }

    [Fact]
    public async Task まだ居ないログインの対応づけは読みにいかない()
    {
        var session = NewSession();
        var page = NewPage(session, "new_login", isNew: true);

        await page.InitializeAsync();

        Assert.Empty(page.Original);
        Assert.All(page.Rows, row => Assert.False(row.IsMapped));
    }

    [Fact]
    public async Task 見ていないページの所属は保存でもそのまま残す()
    {
        var page = NewPage(NewSession(), "app_login", isNew: false);
        await page.InitializeAsync();

        // 選んでいない行はロールを読んでいない。読んでいない行の所属を空にしてはいけない。
        var master = page.Rows.First(row => row.Database == "master");
        master.IsMapped = true;

        var draft = page.ToDrafts().First(row => row.Database == "sales_db");
        Assert.Equal(["db_datareader"], draft.Roles);
    }

    private static LoginUserMappingsViewModel NewPage(FakeDatabaseSession session, string login, bool isNew) =>
        new(
            session,
            login,
            isNew,
            new ListDatabasesUseCase(),
            new ListDatabaseRolesUseCase(),
            new ListLoginUserMappingsUseCase());

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession
        {
            Databases =
            [
                new DatabaseDescriptor(new DatabaseName("sales_db")),
                new DatabaseDescriptor(new DatabaseName("master"), IsSystem: true),
                new DatabaseDescriptor(new DatabaseName("restoring_db"), IsAccessible: false)
            ]
        }
        .WithDatabaseRoles("sales_db", "db_datareader", "app_reader")
        .WithLoginUserMappings(
            new LoginUserMapping(
                new DatabaseName("sales_db"),
                new DatabaseUserName("app_user"),
                new SchemaName("sales"))
            {
                Roles = ["db_datareader"]
            });
}
