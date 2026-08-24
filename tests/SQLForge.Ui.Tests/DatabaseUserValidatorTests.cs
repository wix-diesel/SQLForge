using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ユーザー編集の入力検査。エンティティ（<see cref="DatabaseUserDefinition"/>）は
/// 常に妥当である前提なので、まだ妥当とは限らない入力はここで弾く。
/// </summary>
public class DatabaseUserValidatorTests
{
    [Fact]
    public void 名前が空なら弾く()
    {
        var result = DatabaseUserValidator.Validate(Draft(name: "  "));

        Assert.False(result.IsValid);
        Assert.Equal("ユーザー名を入力してください。", result[DatabaseUserValidator.NameField]);
    }

    [Fact]
    public void 名前が128文字を超えたら弾く()
    {
        var result = DatabaseUserValidator.Validate(Draft(name: new string('a', 129)));

        Assert.False(result.IsValid);
        Assert.Equal("ユーザー名は 128 文字までです。", result[DatabaseUserValidator.NameField]);
    }

    [Fact]
    public void ログインが要る種類でログインが空なら弾く()
    {
        var result = DatabaseUserValidator.Validate(Draft(login: ""));

        Assert.False(result.IsValid);
        Assert.Equal("ログイン名を入力してください。", result[DatabaseUserValidator.LoginField]);
    }

    [Fact]
    public void ログインなしの種類ならログインが空でも通る()
    {
        var result = DatabaseUserValidator.Validate(
            Draft(type: DatabaseUserType.SqlUserWithoutLogin, login: ""));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void 既定のスキーマは空でも通る()
    {
        // 未指定なら SQL Server 側が dbo を当てる。SSMS も空欄のまま作れる。
        Assert.True(DatabaseUserValidator.Validate(Draft(schema: "")).IsValid);
    }

    [Fact]
    public void 既定のスキーマに制御文字があれば弾く()
    {
        var result = DatabaseUserValidator.Validate(Draft(schema: "db\to"));

        Assert.False(result.IsValid);
        Assert.Equal("既定のスキーマに制御文字は使えません。", result[DatabaseUserValidator.DefaultSchemaField]);
    }

    [Fact]
    public void 編集できない種類のユーザーは弾く()
    {
        // 証明書にマップされたユーザーは一覧には出すが、この版では作り替えられない。
        var original = new DatabaseUserDescriptor(
            new DatabaseUserName("##MS_cert_user##"), DatabaseUserType.Certificate);

        var result = DatabaseUserValidator.Validate(Draft() with { Original = original });

        Assert.False(result.IsValid);
        Assert.Equal("この種類のユーザーは編集できません。", result[DatabaseUserValidator.NameField]);
    }

    [Fact]
    public void システムのユーザーは編集できない()
    {
        var original = new DatabaseUserDescriptor(
            new DatabaseUserName("dbo"), DatabaseUserType.SqlUserWithLogin, IsSystem: true);

        var result = DatabaseUserValidator.Validate(Draft(name: "dbo") with { Original = original });

        Assert.False(result.IsValid);
        Assert.Equal("システムのユーザーは変更できません。", result[DatabaseUserValidator.NameField]);
    }

    [Fact]
    public void 妥当な入力はそのまま定義へ変換できる()
    {
        var draft = Draft() with { Roles = ["db_datareader"] };

        Assert.True(DatabaseUserValidator.Validate(draft).IsValid);

        var definition = draft.ToDefinition();

        Assert.Equal("app_user", definition.Name.Value);
        Assert.Equal(DatabaseUserType.SqlUserWithLogin, definition.Type);
        Assert.Equal("app_login", definition.LoginName);
        Assert.Equal("sales", definition.DefaultSchema?.Value);
        Assert.Equal(["db_datareader"], definition.Roles);
    }

    [Fact]
    public void 前後の空白は落として定義へ変換する()
    {
        var definition = Draft(name: "  app_user  ", login: " app_login ", schema: " sales ").ToDefinition();

        Assert.Equal("app_user", definition.Name.Value);
        Assert.Equal("app_login", definition.LoginName);
        Assert.Equal("sales", definition.DefaultSchema?.Value);
    }

    [Fact]
    public void ログインなしの種類ではログイン名を捨てる()
    {
        // 種類を切り替えたあとに前の入力が残っていても、文面へは持ち出さない。
        var definition = Draft(type: DatabaseUserType.SqlUserWithoutLogin, login: "app_login").ToDefinition();

        Assert.Null(definition.LoginName);
    }

    [Fact]
    public void 既存のユーザーからは今の姿を写した下書きを作る()
    {
        var user = new DatabaseUserDescriptor(
            new DatabaseUserName("app_user"),
            DatabaseUserType.SqlUserWithLogin,
            "app_login",
            new SchemaName("sales"))
        {
            Roles = ["db_datareader"]
        };

        var draft = DatabaseUserDraft.FromDescriptor(user);

        Assert.False(draft.IsNew);
        Assert.Same(user, draft.Original);
        Assert.Equal("app_user", draft.Name);
        Assert.Equal("app_login", draft.LoginName);
        Assert.Equal("sales", draft.DefaultSchema);
        Assert.Equal(["db_datareader"], draft.Roles);
    }

    private static DatabaseUserDraft Draft(
        string name = "app_user",
        DatabaseUserType type = DatabaseUserType.SqlUserWithLogin,
        string login = "app_login",
        string schema = "sales") =>
        new()
        {
            Name = name,
            Type = type,
            LoginName = login,
            DefaultSchema = schema
        };
}
