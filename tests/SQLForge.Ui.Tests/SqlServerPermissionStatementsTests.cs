using SQLForge.Domain.Security;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 権限の変更の文面。変わったところだけを出すこと、
/// 付与する権利（GRANT OPTION）が絡むときに取り上げ方が変わることを確かめる。
/// </summary>
public class SqlServerPermissionStatementsTests
{
    private static readonly SecurityPrincipal User = SecurityPrincipal.ForUser(new DatabaseUserName("app_user"));

    private static readonly SecurableReference Schema = new(SecurableKind.Schema, "sales");

    private static readonly SecurableReference Table = new(SecurableKind.Table, "orders", "sales");

    [Fact]
    public void 許可はGRANTで出す()
    {
        var statements = SqlServerPermissionStatements.Changes(
            User,
            original: [],
            desired: [new PermissionEntry(Schema, "SELECT", PermissionState.Granted)]);

        Assert.Equal(["GRANT SELECT ON SCHEMA::[sales] TO [app_user];"], statements);
    }

    [Fact]
    public void 修飾が要るリソースはスキーマ付きで指す()
    {
        var statements = SqlServerPermissionStatements.Changes(
            User,
            original: [],
            desired: [new PermissionEntry(Table, "UPDATE", PermissionState.Granted)]);

        Assert.Equal(["GRANT UPDATE ON OBJECT::[sales].[orders] TO [app_user];"], statements);
    }

    [Fact]
    public void サーバーそのものへの権限は相手を書かない()
    {
        var login = SecurityPrincipal.ForLogin(new ServerLoginName("app_login"));

        var statements = SqlServerPermissionStatements.Changes(
            login,
            original: [],
            desired:
            [
                new PermissionEntry(SecurableReference.Server("srv"), "VIEW ANY DATABASE", PermissionState.Granted)
            ]);

        Assert.Equal(["GRANT VIEW ANY DATABASE TO [app_login];"], statements);
    }

    [Fact]
    public void 変わっていない権限は文面に出さない()
    {
        var granted = new PermissionEntry(Schema, "SELECT", PermissionState.Granted);

        Assert.Empty(SqlServerPermissionStatements.Changes(User, original: [granted], desired: [granted]));
    }

    [Fact]
    public void 望みの姿に出てこない権限は触らない()
    {
        // この版が知らない権限（新しいバージョンで増えたもの）を黙って落とさないため。
        var granted = new PermissionEntry(Schema, "SELECT", PermissionState.Granted);

        Assert.Empty(SqlServerPermissionStatements.Changes(User, original: [granted], desired: []));
    }

    [Fact]
    public void 指定なしへ戻すとREVOKEで取り上げる()
    {
        var statements = SqlServerPermissionStatements.Changes(
            User,
            original: [new PermissionEntry(Schema, "SELECT", PermissionState.Granted)],
            desired: [new PermissionEntry(Schema, "SELECT", PermissionState.Revoked)]);

        Assert.Equal(["REVOKE SELECT ON SCHEMA::[sales] FROM [app_user];"], statements);
    }

    [Fact]
    public void 付与する権利が付いた権限はCASCADEで取り上げる()
    {
        var statements = SqlServerPermissionStatements.Changes(
            User,
            original: [new PermissionEntry(Schema, "SELECT", PermissionState.GrantedWithGrantOption)],
            desired: [new PermissionEntry(Schema, "SELECT", PermissionState.Revoked)]);

        Assert.Equal(["REVOKE SELECT ON SCHEMA::[sales] FROM [app_user] CASCADE;"], statements);
    }

    [Fact]
    public void 付与する権利だけを外すときは先にGRANT_OPTIONを取り上げる()
    {
        // GRANT を出し直しても付与する権利は残るので、先に外すしかない。
        var statements = SqlServerPermissionStatements.Changes(
            User,
            original: [new PermissionEntry(Schema, "SELECT", PermissionState.GrantedWithGrantOption)],
            desired: [new PermissionEntry(Schema, "SELECT", PermissionState.Granted)]);

        Assert.Equal(
            [
                "REVOKE GRANT OPTION FOR SELECT ON SCHEMA::[sales] FROM [app_user] CASCADE;",
                "GRANT SELECT ON SCHEMA::[sales] TO [app_user];"
            ],
            statements);
    }

    [Fact]
    public void 拒否はDENYで出す()
    {
        var statements = SqlServerPermissionStatements.Changes(
            User,
            original: [],
            desired: [new PermissionEntry(Schema, "DELETE", PermissionState.Denied)]);

        Assert.Equal(["DENY DELETE ON SCHEMA::[sales] TO [app_user];"], statements);
    }

    [Fact]
    public void この版が知らない権限は文面へ出さない()
    {
        // 権限の名前は識別子ではないので引用符では囲めない。囲めない以上、出せるのは一覧にあるものだけ。
        var statements = SqlServerPermissionStatements.Changes(
            User,
            original: [],
            desired: [new PermissionEntry(Schema, "SELECT; DROP TABLE x", PermissionState.Granted)]);

        Assert.Empty(statements);
    }
}
