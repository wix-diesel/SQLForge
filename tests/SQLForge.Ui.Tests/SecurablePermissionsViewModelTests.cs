using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 「セキュリティ保護可能なリソース」のページ。読んだ権限がグリッドに並ぶこと、
/// 外した行が「指定なし」として残ること、これから作る相手では読みにいかないこと。
/// </summary>
public class SecurablePermissionsViewModelTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    private static readonly SecurableReference Schema = new(SecurableKind.Schema, "sales");

    [Fact]
    public async Task 読んだ権限はリソースごとの行に組み直す()
    {
        var session = NewSession();
        var page = NewPage(session, "app_user");

        await page.InitializeAsync();

        var row = Assert.Single(page.Securables);
        Assert.Equal("sales", row.DisplayName);
        Assert.Equal("スキーマ", row.KindName);
        Assert.Same(row, page.SelectedSecurable);

        // 一覧に無い権限は「指定なし」で並ぶ。
        Assert.Equal(
            PermissionState.Granted,
            row.Permissions.First(permission => permission.Permission == "SELECT").State);

        Assert.Equal(
            PermissionState.Revoked,
            row.Permissions.First(permission => permission.Permission == "DELETE").State);
    }

    [Fact]
    public async Task 候補から足した行はグリッドに並ぶ()
    {
        var session = NewSession();
        var page = NewPage(session, "app_user");
        await page.InitializeAsync();

        page.SelectedCandidate = new SecurableReference(SecurableKind.Schema, "staging");
        page.AddSecurableCommand.Execute(null);

        Assert.Equal(["sales", "staging"], page.Securables.Select(row => row.DisplayName));
        Assert.Equal("staging", page.SelectedSecurable?.DisplayName);
    }

    [Fact]
    public async Task 同じ行を二重に足さない()
    {
        var session = NewSession();
        var page = NewPage(session, "app_user");
        await page.InitializeAsync();

        page.SelectedCandidate = Schema;
        page.AddSecurableCommand.Execute(null);

        Assert.Single(page.Securables);
    }

    [Fact]
    public async Task 外した行は指定なしとして保存に残る()
    {
        // グリッドから消してしまうと差分に出てこず、外したつもりの権限がサーバーに残る。
        var session = NewSession();
        var page = NewPage(session, "app_user");
        await page.InitializeAsync();

        page.RemoveSecurableCommand.Execute(null);

        Assert.Empty(page.Securables);

        var draft = page.ToDraft(SecurityPrincipal.ForUser(new DatabaseUserName("app_user")));
        var select = draft.Entries.First(entry => entry.Permission == "SELECT");

        Assert.Equal(PermissionState.Revoked, select.State);
    }

    [Fact]
    public async Task これから作る相手には読みにいかない()
    {
        var session = NewSession();
        var page = NewPage(session, name: null);

        await page.InitializeAsync();

        Assert.Empty(page.Securables);
        Assert.Empty(page.Original);
        Assert.Null(page.Error);
    }

    [Fact]
    public async Task 読めなくてもページは開いたままにする()
    {
        var session = NewSession();
        session.SecurityFailure = new InvalidOperationException("VIEW DEFINITION 権限がありません。");

        var page = NewPage(session, "app_user");
        await page.InitializeAsync();

        Assert.Equal("VIEW DEFINITION 権限がありません。", page.Error);
        Assert.True(page.HasError);
    }

    [Fact]
    public void データベースの主体にはデータベース側のリソースだけを出す()
    {
        var page = NewPage(NewSession(), "app_user");

        Assert.Equal(
            ["データベース", "スキーマ", "テーブル", "ストアド プロシージャ"],
            page.KindChoices.Select(choice => choice.DisplayName));
    }

    private static SecurablePermissionsViewModel NewPage(FakeDatabaseSession session, string? name) =>
        new(
            session,
            SecurityPrincipalKind.DatabaseUser,
            name,
            SalesDb,
            new ListPermissionsUseCase(),
            new ListSecurablesUseCase());

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession()
            .WithPermissions("app_user", new PermissionEntry(Schema, "SELECT", PermissionState.Granted))
            .WithSecurables(
                SecurableKind.Database,
                new SecurableReference(SecurableKind.Database, "sales_db"));
}
