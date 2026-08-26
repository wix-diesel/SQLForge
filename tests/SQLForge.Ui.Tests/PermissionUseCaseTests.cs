using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// セキュリティ保護可能なリソースと権限のユースケース。
/// 並べ方と、サーバーへ何を渡すか（渡さないか）をここで固定する。
/// </summary>
public class PermissionUseCaseTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    private static readonly SecurityPrincipal User =
        SecurityPrincipal.ForUser(new DatabaseUserName("app_user"));

    [Fact]
    public async Task 権限はリソース順と権限名順に並ぶ()
    {
        var schema = new SecurableReference(SecurableKind.Schema, "sales");
        var table = new SecurableReference(SecurableKind.Table, "orders", "sales");

        var session = new FakeDatabaseSession().WithPermissions(
            "app_user",
            new PermissionEntry(table, "UPDATE", PermissionState.Granted),
            new PermissionEntry(schema, "SELECT", PermissionState.Granted),
            new PermissionEntry(table, "SELECT", PermissionState.Denied));

        var entries = await new ListPermissionsUseCase().ExecuteAsync(session, User, SalesDb);

        Assert.Equal(
            ["sales:SELECT", "sales.orders:SELECT", "sales.orders:UPDATE"],
            entries.Select(entry => $"{entry.Securable.DisplayName}:{entry.Permission}"));
    }

    [Fact]
    public async Task リソースの候補は名前順に並ぶ()
    {
        var session = new FakeDatabaseSession().WithSecurables(
            SecurableKind.Schema,
            new SecurableReference(SecurableKind.Schema, "staging"),
            new SecurableReference(SecurableKind.Schema, "dbo"));

        var securables = await new ListSecurablesUseCase()
            .ExecuteAsync(session, SecurableKind.Schema, SalesDb);

        Assert.Equal(["dbo", "staging"], securables.Select(securable => securable.Name));
    }

    [Fact]
    public async Task 権限の変更は前後の姿ごとサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();
        var schema = new SecurableReference(SecurableKind.Schema, "sales");
        var before = new PermissionEntry(schema, "SELECT", PermissionState.Granted);
        var after = new PermissionEntry(schema, "SELECT", PermissionState.Denied);

        var result = await new SavePermissionsUseCase().ExecuteAsync(
            session,
            new PermissionDraft
            {
                Principal = User,
                Database = SalesDb,
                Original = [before],
                Entries = [after]
            });

        Assert.True(result.IsValid);
        Assert.Equal(User, session.AppliedPrincipal);
        Assert.Equal([before], session.AppliedOriginalPermissions);
        Assert.Equal([after], session.AppliedPermissions);
    }

    [Fact]
    public async Task この版が知らない権限は送らずに理由を返す()
    {
        var session = new FakeDatabaseSession();

        var result = await new SavePermissionsUseCase().ExecuteAsync(
            session,
            new PermissionDraft
            {
                Principal = User,
                Database = SalesDb,
                Entries =
                [
                    new PermissionEntry(
                        new SecurableReference(SecurableKind.Schema, "sales"),
                        "TAKE EVERYTHING",
                        PermissionState.Granted)
                ]
            });

        Assert.False(result.IsValid);
        Assert.Equal("スキーマ に TAKE EVERYTHING という権限はありません。", result[PermissionValidator.PermissionsField]);
        Assert.Null(session.AppliedPermissions);
    }

    [Fact]
    public async Task データベーススコープの主体に居場所が無ければ送らない()
    {
        var session = new FakeDatabaseSession();

        var result = await new SavePermissionsUseCase().ExecuteAsync(
            session,
            new PermissionDraft { Principal = User });

        Assert.False(result.IsValid);
        Assert.Equal("権限を変更するデータベースが決まっていません。", result[PermissionValidator.DatabaseField]);
        Assert.Null(session.AppliedPermissions);
    }

    [Fact]
    public async Task サーバースコープの主体にデータベースのリソースは付けられない()
    {
        var session = new FakeDatabaseSession();

        var result = await new SavePermissionsUseCase().ExecuteAsync(
            session,
            new PermissionDraft
            {
                Principal = SecurityPrincipal.ForLogin(new ServerLoginName("app_login")),
                Entries =
                [
                    new PermissionEntry(
                        new SecurableReference(SecurableKind.Schema, "sales"),
                        "SELECT",
                        PermissionState.Granted)
                ]
            });

        Assert.False(result.IsValid);
        Assert.Equal(
            "スキーマ には、この主体の権限を付けられません。",
            result[PermissionValidator.PermissionsField]);
    }
}
