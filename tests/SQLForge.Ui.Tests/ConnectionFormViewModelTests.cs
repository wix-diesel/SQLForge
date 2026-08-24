using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Linux;
using SQLForge.Infrastructure.Windows;
using SQLForge.Ui.Presentation;
using SQLForge.Ui.ViewModels;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>入力欄の振る舞い（既定値の追随・検証・接続 URL・認証方式ごとの見え方）。</summary>
public class ConnectionFormViewModelTests
{
    [Fact]
    public void ドライバーを変えるとポートとデータベースが既定値になる()
    {
        var form = NewForm(driver: DatabaseDriver.PostgreSql);

        Assert.Equal("5432", form.Port);
        Assert.Equal("postgres", form.Database);

        form.Driver = DatabaseDriver.MySql;

        Assert.Equal("3306", form.Port);
        Assert.Equal("mysql", form.Database);
    }

    [Fact]
    public void 本番タグを選ぶと読み取り専用が既定で入る()
    {
        var form = NewForm();
        form.Environment = EnvironmentChoiceViewModel.For(EnvironmentTag.Local);

        Assert.False(form.IsReadOnly);

        form.Environment = EnvironmentChoiceViewModel.For(EnvironmentTag.Production);

        Assert.True(form.IsReadOnly);
        Assert.False(form.IsUnsafeWriteAccess);

        form.IsReadOnly = false;

        Assert.True(form.IsUnsafeWriteAccess);
    }

    [Fact]
    public void ファイル接続ではホスト以外の接続先欄が消える()
    {
        var form = NewForm(driver: DatabaseDriver.Sqlite);

        Assert.False(form.SupportsNetworkAddress);
        Assert.False(form.RequiresPassword);
        Assert.False(form.RequiresUserName);
        Assert.False(form.UsesIntegratedAuthentication);
        Assert.Equal("ファイル", form.HostLabel);
    }

    [Fact]
    public void 入力から接続URLが組み立てられる()
    {
        var form = FilledForm();

        Assert.Equal(
            "postgresql://analyst_ro@10.2.0.14:5432/sales_db?sslmode=verify-full&application_name=sqlforge",
            form.Url);

        var scheme = form.UrlParts.First();
        Assert.Equal("postgresql://", scheme.Text);
        Assert.True(scheme.IsScheme);
        Assert.Contains(form.UrlParts, part => part.IsDatabase && part.Text == "sales_db");
        Assert.Contains(form.UrlParts, part => part.IsParameterName && part.Text == "sslmode");
    }

    [Fact]
    public void 未入力の欄は検証で弾かれる()
    {
        var form = NewForm(driver: DatabaseDriver.PostgreSql);

        var validation = ConnectionValidator.Validate(form.ToDraft());

        Assert.False(validation.IsValid);
        Assert.NotNull(validation[ConnectionValidator.NameField]);
        Assert.NotNull(validation[ConnectionValidator.HostField]);
        Assert.NotNull(validation[ConnectionValidator.UserField]);
    }

    [Fact]
    public void 保存済み接続を読み込むと入力欄に写る()
    {
        var profile = SQLForge.Infrastructure.Connections.SeedConnections.Create().First();
        var form = NewForm();

        form.Load(profile);

        Assert.Equal(profile.Name, form.Name);
        Assert.Equal(profile.Target.Address.Host, form.Host);
        Assert.Equal(profile.Environment, form.Environment.Tag);
        Assert.True(ConnectionValidator.Validate(form.ToDraft()).IsValid);
    }

    [Fact]
    public void OS統合認証を選ぶと利用者名とパスワードの欄が消える()
    {
        var form = FilledForm();

        form.Authentication = AuthenticationChoice.For(AuthenticationMethod.Integrated);

        Assert.True(form.UsesIntegratedAuthentication);
        Assert.False(form.RequiresUserName);
        Assert.False(form.RequiresPassword);
    }

    [Fact]
    public void OS統合認証では繋ぐOSアカウント名を出す()
    {
        var form = FilledForm(new WindowsPlatformProfile());

        form.Authentication = AuthenticationChoice.For(AuthenticationMethod.Integrated);

        Assert.False(string.IsNullOrWhiteSpace(form.IntegratedAccountName));
    }

    [Fact]
    public void OS統合認証ではKerberosが要るOSでだけ注意書きを出す()
    {
        var linux = FilledForm(new LinuxPlatformProfile());
        var windows = FilledForm(new WindowsPlatformProfile());

        linux.Authentication = AuthenticationChoice.For(AuthenticationMethod.Integrated);
        windows.Authentication = AuthenticationChoice.For(AuthenticationMethod.Integrated);

        Assert.True(linux.ShowsKerberosNotice);
        Assert.False(windows.ShowsKerberosNotice);
    }

    [Fact]
    public void OS統合認証では利用者名を検証しないし接続URLにも載せない()
    {
        var form = FilledForm();
        form.Driver = DatabaseDriver.SqlServer;
        form.Name = "sqlforge-test";
        form.Host = "db.internal";
        form.Database = "sales_db";
        form.User = string.Empty;
        form.Tls = TlsChoice.For(TlsMode.Require);
        form.Authentication = AuthenticationChoice.For(AuthenticationMethod.Integrated);

        Assert.True(ConnectionValidator.Validate(form.ToDraft()).IsValid);
        Assert.DoesNotContain("@", form.Url, StringComparison.Ordinal);
        Assert.Contains("integrated_security=true", form.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void OS統合認証で保存すると利用者名は残らない()
    {
        // 打ってあった利用者名は使われないので、保存する内容にも持ち越さない。
        var form = FilledForm();
        form.Name = "sqlforge-test";
        form.Authentication = AuthenticationChoice.For(AuthenticationMethod.Integrated);

        Assert.Equal("analyst_ro", form.User);
        Assert.Empty(form.ToDraft().ToProfile().Credentials.UserName);
    }

    private static ConnectionFormViewModel NewForm(
        IPlatformProfile? platform = null,
        DatabaseDriver? driver = null)
    {
        var form = new ConnectionFormViewModel(platform ?? new LinuxPlatformProfile());

        if (driver is not null)
        {
            form.Driver = driver;
        }

        return form;
    }

    private static ConnectionFormViewModel FilledForm(IPlatformProfile? platform = null)
    {
        var form = NewForm(platform, DatabaseDriver.PostgreSql);
        form.Host = "10.2.0.14";
        form.Database = "sales_db";
        form.User = "analyst_ro";
        form.Tls = TlsChoice.For(TlsMode.VerifyFull);
        return form;
    }
}
