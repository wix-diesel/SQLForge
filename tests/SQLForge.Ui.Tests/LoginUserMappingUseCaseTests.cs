using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ユーザー マッピング。ログインの保存に相乗りして、
/// データベースごとのユーザーを揃えるところまでを確かめる。
/// </summary>
public class LoginUserMappingUseCaseTests
{
    [Fact]
    public async Task 対応づけはデータベース名順に並ぶ()
    {
        var session = new FakeDatabaseSession().WithLoginUserMappings(
            Mapping("staging_db"),
            Mapping("sales_db"));

        var mappings = await new ListLoginUserMappingsUseCase()
            .ExecuteAsync(session, new ServerLoginName("app_login"));

        Assert.Equal(["sales_db", "staging_db"], mappings.Select(mapping => mapping.Database.Value));
    }

    [Fact]
    public async Task ログインの保存で対応づけも揃える()
    {
        var session = new FakeDatabaseSession();
        var original = Mapping("sales_db");

        var result = await new SaveServerLoginUseCase().ExecuteAsync(
            session,
            Draft() with
            {
                OriginalMappings = [original],
                Mappings =
                [
                    new LoginUserMappingDraft
                    {
                        Database = "staging_db",
                        IsMapped = true,
                        UserName = "app_user",
                        DefaultSchema = string.Empty
                    },
                    LoginUserMappingDraft.Unmapped("sales_db")
                ]
            });

        Assert.True(result.IsValid);
        Assert.Equal([original], session.AppliedOriginalMappings);

        // チェックの付いた行だけが望みの姿になる。外した行は「そこには居ない」を表す。
        var applied = Assert.Single(session.AppliedMappings!);
        Assert.Equal("staging_db", applied.Database.Value);
        Assert.Equal("app_user", applied.User.Value);
    }

    [Fact]
    public async Task ユーザー名を書かなければログイン名をそのまま使う()
    {
        var session = new FakeDatabaseSession();

        await new SaveServerLoginUseCase().ExecuteAsync(
            session,
            Draft() with
            {
                Mappings =
                [
                    new LoginUserMappingDraft
                    {
                        Database = "sales_db",
                        IsMapped = true,
                        UserName = string.Empty,
                        DefaultSchema = string.Empty
                    }
                ]
            });

        Assert.Equal("app_login", Assert.Single(session.AppliedMappings!).User.Value);
    }

    [Fact]
    public async Task ページを開かなかった編集では対応づけに触らない()
    {
        // 前後とも空のまま送ると「すべての対応づけを外す」になってしまう。
        var session = new FakeDatabaseSession();

        await new SaveServerLoginUseCase().ExecuteAsync(session, Draft());

        Assert.Null(session.AppliedMappings);
    }

    [Fact]
    public async Task 対応づけのユーザー名がおかしければ送らずに理由を返す()
    {
        var session = new FakeDatabaseSession();

        var result = await new SaveServerLoginUseCase().ExecuteAsync(
            session,
            Draft() with
            {
                Mappings =
                [
                    new LoginUserMappingDraft
                    {
                        Database = "sales_db",
                        IsMapped = true,
                        UserName = new string('u', 129),
                        DefaultSchema = string.Empty
                    }
                ]
            });

        Assert.False(result.IsValid);
        Assert.Equal(
            "sales_db のユーザー名は 128 文字までです。",
            result[ServerLoginValidator.MappingField]);
        Assert.Null(session.CreatedLogin);
    }

    private static ServerLoginDraft Draft() =>
        ServerLoginDraft.ForNewLogin() with
        {
            Name = "app_login",
            Password = "pa55word!",
            PasswordConfirmation = "pa55word!"
        };

    private static LoginUserMapping Mapping(string database) =>
        new(new DatabaseName(database), new DatabaseUserName("app_user"));
}
