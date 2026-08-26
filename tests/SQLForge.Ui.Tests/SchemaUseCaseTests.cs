using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// スキーマの追加・所有者の付け替え・削除のユースケース。
/// 名前を変えられないこと、システムのスキーマを触らせないことをここで固定する。
/// </summary>
public class SchemaUseCaseTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [Fact]
    public async Task 新しいスキーマは作成としてサーバーへ渡る()
    {
        var session = new FakeDatabaseSession();

        var result = await new SaveSchemaUseCase().ExecuteAsync(
            session,
            SalesDb,
            new SchemaDraft { Name = " sales ", Owner = " app_user " });

        Assert.True(result.IsValid);
        Assert.Equal("sales", session.CreatedSchema?.Name.Value);
        Assert.Equal("app_user", session.CreatedSchema?.Owner);
    }

    [Fact]
    public async Task 所有者を空のままにすればサーバーに任せる()
    {
        var session = new FakeDatabaseSession();

        await new SaveSchemaUseCase().ExecuteAsync(
            session,
            SalesDb,
            new SchemaDraft { Name = "sales", Owner = string.Empty });

        Assert.Null(session.CreatedSchema?.Owner);
    }

    [Fact]
    public async Task 既存のスキーマは所有者の付け替えとして渡る()
    {
        var session = new FakeDatabaseSession();
        var original = new SchemaDescriptor(new SchemaName("sales"), Owner: "dbo");

        var result = await new SaveSchemaUseCase().ExecuteAsync(
            session,
            SalesDb,
            SchemaDraft.FromDescriptor(original) with { Owner = "app_user" });

        Assert.True(result.IsValid);
        Assert.Null(session.CreatedSchema);
        Assert.Equal(original, session.AlteredOriginalSchema);
        Assert.Equal("app_user", session.AlteredSchema?.Owner);
    }

    [Fact]
    public async Task スキーマの名前は変えられない()
    {
        // SQL Server にスキーマの名前を変える文面は無い（作り直して中身を移すしかない）。
        var session = new FakeDatabaseSession();
        var original = new SchemaDescriptor(new SchemaName("sales"));

        var result = await new SaveSchemaUseCase().ExecuteAsync(
            session,
            SalesDb,
            SchemaDraft.FromDescriptor(original) with { Name = "sales_new" });

        Assert.False(result.IsValid);
        Assert.Equal(
            "スキーマの名前は変更できません。所有者だけを変えられます。",
            result[SchemaValidator.NameField]);
        Assert.Null(session.AlteredSchema);
    }

    [Fact]
    public async Task システムのスキーマは変更も削除もさせない()
    {
        var session = new FakeDatabaseSession();
        var system = new SchemaDescriptor(new SchemaName("sys"), IsSystem: true);

        var result = await new SaveSchemaUseCase().ExecuteAsync(
            session,
            SalesDb,
            SchemaDraft.FromDescriptor(system));

        Assert.False(result.IsValid);
        Assert.Equal("システムのスキーマは変更できません。", result[SchemaValidator.NameField]);

        await Assert.ThrowsAsync<SchemaRejectedException>(() =>
            new DropSchemaUseCase().ExecuteAsync(session, SalesDb, system));

        Assert.Null(session.DroppedSchema);
    }

    [Fact]
    public async Task スキーマの削除はそのまま渡る()
    {
        var session = new FakeDatabaseSession();

        await new DropSchemaUseCase().ExecuteAsync(
            session,
            SalesDb,
            new SchemaDescriptor(new SchemaName("sales")));

        Assert.Equal("sales", session.DroppedSchema?.Value);
    }
}
