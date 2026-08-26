using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ログイン編集の入力検査。エンティティ（<see cref="ServerLoginDefinition"/>）は
/// 常に妥当である前提なので、まだ妥当とは限らない入力はここで弾く。
/// </summary>
public class ServerLoginValidatorTests
{
    [Fact]
    public void 名前が空なら弾く()
    {
        var result = ServerLoginValidator.Validate(Draft(name: "  "));

        Assert.False(result.IsValid);
        Assert.Equal("ログイン名を入力してください。", result[ServerLoginValidator.NameField]);
    }

    [Fact]
    public void 名前が128文字を超えたら弾く()
    {
        var result = ServerLoginValidator.Validate(Draft(name: new string('a', 129)));

        Assert.False(result.IsValid);
        Assert.Equal("ログイン名は 128 文字までです。", result[ServerLoginValidator.NameField]);
    }

    [Fact]
    public void 新しいSQL認証のログインはパスワードが要る()
    {
        var result = ServerLoginValidator.Validate(Draft(password: string.Empty, confirmation: string.Empty));

        Assert.False(result.IsValid);
        Assert.Equal("パスワードを入力してください。", result[ServerLoginValidator.PasswordField]);
    }

    [Fact]
    public void 確認の入力が違えば弾く()
    {
        var result = ServerLoginValidator.Validate(Draft(confirmation: "typo"));

        Assert.False(result.IsValid);
        Assert.Equal("パスワードが一致しません。", result[ServerLoginValidator.ConfirmationField]);
    }

    [Fact]
    public void Windows認証ならパスワードは要らない()
    {
        var draft = Draft(name: @"CONTOSO\app", password: string.Empty, confirmation: string.Empty)
            with { Type = ServerLoginType.WindowsUser };

        Assert.True(ServerLoginValidator.Validate(draft).IsValid);
    }

    [Fact]
    public void 編集ではパスワードが空でも通る()
    {
        // 空欄は「今のパスワードのまま」。サーバーからは読めないので、写して見せることもできない。
        var draft = ServerLoginDraft.FromDescriptor(Original());

        Assert.True(ServerLoginValidator.Validate(draft).IsValid);
    }

    [Fact]
    public void 期限だけを適用しようとしたら弾く()
    {
        var draft = Draft() with { EnforcePolicy = false, EnforceExpiration = true };

        var result = ServerLoginValidator.Validate(draft);

        Assert.False(result.IsValid);
        Assert.Equal(
            "パスワードの期限を適用するには、パスワード ポリシーの適用が要ります。",
            result[ServerLoginValidator.PolicyField]);
    }

    [Fact]
    public void 次回変更を求めるならポリシーと期限の適用が要る()
    {
        var draft = Draft() with { EnforcePolicy = false, EnforceExpiration = false, MustChangePassword = true };

        var result = ServerLoginValidator.Validate(draft);

        Assert.False(result.IsValid);
        Assert.Equal(
            "次回ログイン時のパスワード変更を求めるには、パスワード ポリシーと期限の適用が要ります。",
            result[ServerLoginValidator.PolicyField]);
    }

    [Fact]
    public void 編集で次回変更を求めるなら新しいパスワードが要る()
    {
        var draft = ServerLoginDraft.FromDescriptor(Original()) with { MustChangePassword = true };

        var result = ServerLoginValidator.Validate(draft);

        Assert.False(result.IsValid);
        Assert.Equal(
            "次回ログイン時のパスワード変更を求めるには、新しいパスワードが要ります。",
            result[ServerLoginValidator.PasswordField]);
    }

    [Fact]
    public void Windows認証のログインは名前を変えられない()
    {
        var original = new ServerLoginDescriptor(new ServerLoginName(@"CONTOSO\app"), ServerLoginType.WindowsUser);
        var draft = ServerLoginDraft.FromDescriptor(original) with { Name = @"CONTOSO\other" };

        var result = ServerLoginValidator.Validate(draft);

        Assert.False(result.IsValid);
        Assert.Equal("Windows 認証のログインの名前は変更できません。", result[ServerLoginValidator.NameField]);
    }

    [Fact]
    public void システムのログインは編集できない()
    {
        var original = Original() with { Name = new ServerLoginName("sa"), IsSystem = true };
        var draft = ServerLoginDraft.FromDescriptor(original);

        var result = ServerLoginValidator.Validate(draft);

        Assert.False(result.IsValid);
        Assert.Equal("システムのログインは変更できません。", result[ServerLoginValidator.NameField]);
    }

    [Fact]
    public void 編集できない種類のログインは弾く()
    {
        // 証明書にマップされたログインは一覧には出すが、この版では作り替えられない。
        var original = new ServerLoginDescriptor(
            new ServerLoginName("##MS_PolicyEventProcessingLogin##"), ServerLoginType.Certificate);

        var result = ServerLoginValidator.Validate(ServerLoginDraft.FromDescriptor(original));

        Assert.False(result.IsValid);
        Assert.Equal("この種類のログインは編集できません。", result[ServerLoginValidator.NameField]);
    }

    [Fact]
    public void 妥当な入力はそのまま定義へ変換できる()
    {
        var draft = Draft() with { Roles = ["dbcreator"], IsDisabled = true };

        Assert.True(ServerLoginValidator.Validate(draft).IsValid);

        var definition = draft.ToDefinition();

        Assert.Equal("app_login", definition.Name.Value);
        Assert.Equal(ServerLoginType.SqlLogin, definition.Type);
        Assert.Equal("p@ssw0rd", definition.Password);
        Assert.Equal(ServerLoginPasswordPolicy.Default, definition.PasswordPolicy);
        Assert.Equal("sales_db", definition.DefaultDatabase?.Value);
        Assert.True(definition.IsDisabled);
        Assert.Equal(["dbcreator"], definition.Roles);
    }

    [Fact]
    public void 名前の前後の空白は落としてパスワードはそのまま渡す()
    {
        // パスワードは前後の空白も値のうち。名前と同じように削ると、入れたものと違う値で作られる。
        var draft = Draft(name: "  app_login  ", password: " p@ss ", confirmation: " p@ss ");

        var definition = draft.ToDefinition();

        Assert.Equal("app_login", definition.Name.Value);
        Assert.Equal(" p@ss ", definition.Password);
    }

    [Fact]
    public void Windows認証ではパスワードも規則も持たせない()
    {
        // 認証方式を切り替えたあとに前の入力が残っていても、文面へは持ち出さない。
        var draft = Draft(name: @"CONTOSO\app") with { Type = ServerLoginType.WindowsUser };

        var definition = draft.ToDefinition();

        Assert.Null(definition.Password);
        Assert.Null(definition.PasswordPolicy);
        Assert.False(definition.MustChangePassword);
    }

    [Fact]
    public void 定義はパスワードを書き出さない()
    {
        // レコードの既定の ToString は全プロパティを並べる。例外やログへ平文が漏れる経路を塞ぐ。
        var definition = Draft().ToDefinition();

        Assert.DoesNotContain("p@ssw0rd", definition.ToString(), StringComparison.Ordinal);
        Assert.Contains("app_login", definition.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void 下書きもパスワードを書き出さない()
    {
        // 下書きは UI とユースケースの境目を渡り歩くので、定義よりむしろ露出しやすい。
        var draft = Draft();

        Assert.DoesNotContain("p@ssw0rd", draft.ToString(), StringComparison.Ordinal);
        Assert.Contains("app_login", draft.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void 既存のログインからは今の姿を写した下書きを作る()
    {
        var login = Original() with { Roles = ["dbcreator"], IsDisabled = true };

        var draft = ServerLoginDraft.FromDescriptor(login);

        Assert.False(draft.IsNew);
        Assert.Same(login, draft.Original);
        Assert.Equal("app_login", draft.Name);
        Assert.Equal(string.Empty, draft.Password);
        Assert.True(draft.EnforcePolicy);
        Assert.True(draft.EnforceExpiration);
        Assert.False(draft.MustChangePassword);
        Assert.Equal("master", draft.DefaultDatabase);
        Assert.True(draft.IsDisabled);
        Assert.Equal(["dbcreator"], draft.Roles);
    }

    [Fact]
    public void 新規の初期値はSSMSと同じくポリシーも期限も次回変更も適用する()
    {
        var draft = ServerLoginDraft.ForNewLogin();

        Assert.True(draft.IsNew);
        Assert.Equal(ServerLoginType.SqlLogin, draft.Type);
        Assert.True(draft.EnforcePolicy);
        Assert.True(draft.EnforceExpiration);
        Assert.True(draft.MustChangePassword);
        Assert.False(draft.IsDisabled);
    }

    private static ServerLoginDraft Draft(
        string name = "app_login",
        string password = "p@ssw0rd",
        string? confirmation = null) =>
        new()
        {
            Name = name,
            Type = ServerLoginType.SqlLogin,
            Password = password,
            PasswordConfirmation = confirmation ?? password,
            EnforcePolicy = true,
            EnforceExpiration = true,
            MustChangePassword = false,
            DefaultDatabase = "sales_db"
        };

    private static ServerLoginDescriptor Original() =>
        new(new ServerLoginName("app_login"), ServerLoginType.SqlLogin, new DatabaseName("master"))
        {
            PasswordPolicy = ServerLoginPasswordPolicy.Default
        };
}
