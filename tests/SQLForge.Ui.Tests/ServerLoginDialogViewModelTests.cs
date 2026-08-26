using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ログインのプロパティ ダイアログ。SSMS の「ログイン」に合わせ、
/// 新規と編集で触れる項目が変わることと、保存の中身を確かめる。
/// </summary>
public class ServerLoginDialogViewModelTests
{
    [Fact]
    public async Task 新規は既定でSQL認証のログイン()
    {
        var dialog = await NewDialogAsync(NewSession());

        Assert.True(dialog.IsNew);
        Assert.Equal("新しいログイン", dialog.Title);
        Assert.True(dialog.CanChangeType);
        Assert.True(dialog.CanRename);
        Assert.Equal(ServerLoginType.SqlLogin, dialog.SelectedType.Value);
        Assert.True(dialog.RequiresPassword);
        Assert.False(dialog.CanKeepPassword);
        Assert.True(dialog.EnforcePolicy);
        Assert.True(dialog.EnforceExpiration);
        Assert.True(dialog.MustChangePassword);
        Assert.True(dialog.IsLoginEnabled);
    }

    [Fact]
    public async Task 選べる認証方式はSSMSと同じ3つ()
    {
        var dialog = await NewDialogAsync(NewSession());

        Assert.Equal(
            ["SQL Server 認証のログイン", "Windows 認証のログイン", "Windows 認証のグループ"],
            dialog.TypeChoices.Select(choice => choice.DisplayName));
    }

    [Fact]
    public async Task Windows認証を選ぶとパスワード欄を使わない()
    {
        var dialog = await NewDialogAsync(NewSession());

        dialog.SelectedType = dialog.TypeChoices.First(choice => choice.Value == ServerLoginType.WindowsUser);

        Assert.False(dialog.RequiresPassword);
    }

    [Fact]
    public async Task 既定のデータベースとサーバーロールはサーバーから読む()
    {
        var dialog = await NewDialogAsync(NewSession());

        // 既定のデータベースには master も選べる（むしろ SSMS の初期値）。
        // 並びはツリーと同じで、利用者が作ったものが先、エンジンのものが後ろ。
        Assert.Equal(["sales_db", "master"], dialog.DatabaseChoices);
        Assert.Equal(["dbcreator", "sysadmin"], dialog.Roles.Select(role => role.Name));
        Assert.All(dialog.Roles, role => Assert.False(role.IsMember));
    }

    [Fact]
    public async Task 編集では今の姿を写して認証方式は変えさせない()
    {
        var login = new ServerLoginDescriptor(
            new ServerLoginName("app_login"),
            ServerLoginType.SqlLogin,
            new DatabaseName("sales_db"),
            IsDisabled: true)
        {
            Roles = ["dbcreator"],
            PasswordPolicy = new ServerLoginPasswordPolicy(enforcePolicy: true, enforceExpiration: false)
        };

        var dialog = await NewDialogAsync(NewSession(), login);

        Assert.False(dialog.IsNew);
        Assert.Equal("ログイン — app_login", dialog.Title);
        Assert.False(dialog.CanChangeType);
        Assert.Equal("app_login", dialog.Name);
        Assert.Equal("sales_db", dialog.DefaultDatabase);
        Assert.True(dialog.EnforcePolicy);
        Assert.False(dialog.EnforceExpiration);
        Assert.False(dialog.IsLoginEnabled);

        // パスワードはサーバーから読めない。空のまま保存すれば今のものが残る。
        Assert.Equal(string.Empty, dialog.Password);
        Assert.True(dialog.CanKeepPassword);

        Assert.True(dialog.Roles.Single(role => role.Name == "dbcreator").IsMember);
        Assert.False(dialog.Roles.Single(role => role.Name == "sysadmin").IsMember);
    }

    [Fact]
    public async Task Windows認証のログインは名前を変えさせない()
    {
        var login = new ServerLoginDescriptor(new ServerLoginName(@"CONTOSO\app"), ServerLoginType.WindowsUser);

        var dialog = await NewDialogAsync(NewSession(), login);

        Assert.False(dialog.CanRename);
        Assert.False(dialog.RequiresPassword);
    }

    [Fact]
    public async Task 保存すると入力とチェックしたロールがサーバーへ渡り閉じる()
    {
        var session = NewSession();
        var dialog = await NewDialogAsync(session);
        var closed = 0;
        var saved = false;
        dialog.CloseRequested += (_, result) => { closed++; saved = result; };

        dialog.Name = "app_login";
        dialog.Password = "p@ssw0rd";
        dialog.PasswordConfirmation = "p@ssw0rd";
        dialog.DefaultDatabase = "sales_db";
        dialog.IsLoginEnabled = false;
        dialog.Roles.First(role => role.Name == "dbcreator").IsMember = true;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal("app_login", session.CreatedLogin?.Name.Value);
        Assert.Equal("p@ssw0rd", session.CreatedLogin?.Password);
        Assert.True(session.CreatedLogin?.MustChangePassword);
        Assert.Equal("sales_db", session.CreatedLogin?.DefaultDatabase?.Value);
        Assert.True(session.CreatedLogin?.IsDisabled);
        Assert.Equal(["dbcreator"], session.CreatedLogin?.Roles);
        Assert.Equal(1, closed);
        Assert.True(saved);
    }

    [Fact]
    public async Task 入力が妥当でなければ理由を出して閉じない()
    {
        var session = NewSession();
        var dialog = await NewDialogAsync(session);
        var closed = false;
        dialog.CloseRequested += (_, _) => closed = true;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal("ログイン名を入力してください。", dialog.ErrorMessage);
        Assert.True(dialog.HasNameError);
        Assert.Null(session.CreatedLogin);
        Assert.False(closed);
    }

    [Fact]
    public async Task 確認の入力が違えば理由を出して閉じない()
    {
        var session = NewSession();
        var dialog = await NewDialogAsync(session);

        dialog.Name = "app_login";
        dialog.Password = "p@ssw0rd";
        dialog.PasswordConfirmation = "typo";

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal("パスワードが一致しません。", dialog.ErrorMessage);
        Assert.True(dialog.HasConfirmationError);
        Assert.Null(session.CreatedLogin);
    }

    [Fact]
    public async Task サーバーが断ったら理由を出して閉じない()
    {
        var session = NewSession();
        session.SecurityFailure = new InvalidOperationException("ALTER ANY LOGIN 権限がありません。");
        var dialog = await NewDialogAsync(session);
        var closed = false;
        dialog.CloseRequested += (_, _) => closed = true;

        dialog.Name = "app_login";
        dialog.Password = "p@ssw0rd";
        dialog.PasswordConfirmation = "p@ssw0rd";

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal("ALTER ANY LOGIN 権限がありません。", dialog.ErrorMessage);
        Assert.False(closed);
    }

    [Fact]
    public async Task キャンセルは保存せずに閉じる()
    {
        var session = NewSession();
        var dialog = await NewDialogAsync(session);
        bool? saved = null;
        dialog.CloseRequested += (_, result) => saved = result;

        dialog.CancelCommand.Execute(null);

        Assert.False(saved);
        Assert.Null(session.CreatedLogin);
    }

    private static async Task<ServerLoginDialogViewModel> NewDialogAsync(
        FakeDatabaseSession session,
        ServerLoginDescriptor? login = null)
    {
        var dialog = SecurityDialogs.Login(session, login);

        await dialog.InitializeAsync();

        return dialog;
    }

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession
        {
            Databases =
            [
                new DatabaseDescriptor(new DatabaseName("sales_db")),
                new DatabaseDescriptor(new DatabaseName("master"), IsSystem: true)
            ]
        }
        .WithServerRoles("sysadmin", "dbcreator");
}
