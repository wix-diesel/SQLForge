using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.ViewModels.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ユーザーのプロパティ ダイアログ。SSMS の「データベース ユーザー」に合わせ、
/// 新規と編集で触れる項目が変わることと、保存の中身を確かめる。
/// </summary>
public class DatabaseUserDialogViewModelTests
{
    [Fact]
    public async Task 新規は既定でログインありのSQLユーザー()
    {
        var dialog = await NewDialogAsync(NewSession());

        Assert.True(dialog.IsNew);
        Assert.Equal("新しいデータベース ユーザー", dialog.Title);
        Assert.True(dialog.CanChangeType);
        Assert.Equal(DatabaseUserType.SqlUserWithLogin, dialog.SelectedType.Value);
        Assert.True(dialog.RequiresLogin);
        Assert.Equal(string.Empty, dialog.Name);
    }

    [Fact]
    public async Task 選べる種類はSSMSと同じ4つ()
    {
        var dialog = await NewDialogAsync(NewSession());

        Assert.Equal(
            ["SQL ユーザー（ログインあり）", "SQL ユーザー（ログインなし）", "Windows ユーザー", "Windows グループ"],
            dialog.TypeChoices.Select(choice => choice.DisplayName));
    }

    [Fact]
    public async Task ログインなしを選ぶとログイン欄を使わない()
    {
        var dialog = await NewDialogAsync(NewSession());

        dialog.SelectedType = dialog.TypeChoices.First(
            choice => choice.Value == DatabaseUserType.SqlUserWithoutLogin);

        Assert.False(dialog.RequiresLogin);
    }

    [Fact]
    public async Task 既定のスキーマとロールはデータベースから読む()
    {
        var dialog = await NewDialogAsync(NewSession());

        Assert.Equal(["dbo", "sales"], dialog.SchemaChoices);
        Assert.Equal(["app_reader", "db_datareader"], dialog.Roles.Select(role => role.Name));
        Assert.All(dialog.Roles, role => Assert.False(role.IsMember));
    }

    [Fact]
    public async Task 編集では今の姿を写して種類は変えさせない()
    {
        var user = new DatabaseUserDescriptor(
            new DatabaseUserName("app_user"),
            DatabaseUserType.SqlUserWithLogin,
            "app_login",
            new SchemaName("sales"))
        {
            Roles = ["db_datareader"]
        };

        var dialog = await NewDialogAsync(NewSession(), user);

        Assert.False(dialog.IsNew);
        Assert.Equal("データベース ユーザー — app_user", dialog.Title);
        Assert.False(dialog.CanChangeType);
        Assert.Equal("app_user", dialog.Name);
        Assert.Equal("app_login", dialog.LoginName);
        Assert.Equal("sales", dialog.DefaultSchema);
        Assert.True(dialog.Roles.Single(role => role.Name == "db_datareader").IsMember);
        Assert.False(dialog.Roles.Single(role => role.Name == "app_reader").IsMember);
    }

    [Fact]
    public async Task 保存すると入力とチェックしたロールがサーバーへ渡り閉じる()
    {
        var session = NewSession();
        var dialog = await NewDialogAsync(session);
        var closed = 0;
        var saved = false;
        dialog.CloseRequested += (_, result) => { closed++; saved = result; };

        dialog.Name = "app_user";
        dialog.LoginName = "app_login";
        dialog.DefaultSchema = "sales";
        dialog.Roles.First(role => role.Name == "db_datareader").IsMember = true;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal("app_user", session.CreatedUser?.Name.Value);
        Assert.Equal("app_login", session.CreatedUser?.LoginName);
        Assert.Equal("sales", session.CreatedUser?.DefaultSchema?.Value);
        Assert.Equal(["db_datareader"], session.CreatedUser?.Roles);
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

        Assert.Equal("ユーザー名を入力してください。", dialog.ErrorMessage);
        Assert.True(dialog.HasNameError);
        Assert.Null(session.CreatedUser);
        Assert.False(closed);
    }

    [Fact]
    public async Task サーバーが断ったら理由を出して閉じない()
    {
        var session = NewSession();
        session.SecurityFailure = new InvalidOperationException("ALTER ANY USER 権限がありません。");
        var dialog = await NewDialogAsync(session);
        var closed = false;
        dialog.CloseRequested += (_, _) => closed = true;

        dialog.Name = "app_user";
        dialog.LoginName = "app_login";

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal("ALTER ANY USER 権限がありません。", dialog.ErrorMessage);
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
        Assert.Null(session.CreatedUser);
    }

    private static async Task<DatabaseUserDialogViewModel> NewDialogAsync(
        FakeDatabaseSession session,
        DatabaseUserDescriptor? user = null)
    {
        var dialog = new DatabaseUserDialogViewModel(
            session,
            new DatabaseName("sales_db"),
            user,
            new ListSchemasUseCase(),
            new ListDatabaseRolesUseCase(),
            new SaveDatabaseUserUseCase());

        await dialog.InitializeAsync();

        return dialog;
    }

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession()
            .WithSchemas("sales_db",
                new SchemaDescriptor(new SchemaName("dbo")),
                new SchemaDescriptor(new SchemaName("sales")),
                new SchemaDescriptor(new SchemaName("sys"), IsSystem: true))
            .WithDatabaseRoles("sales_db", "db_datareader", "app_reader");
}
