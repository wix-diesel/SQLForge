using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Ui.Presentation;
using SQLForge.Ui.ViewModels;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>入力欄の振る舞い（既定値の追随・検証・接続 URL）。</summary>
public class ConnectionFormViewModelTests
{
    [Fact]
    public void ドライバーを変えるとポートとデータベースが既定値になる()
    {
        var form = new ConnectionFormViewModel { Driver = DatabaseDriver.PostgreSql };

        Assert.Equal("5432", form.Port);
        Assert.Equal("postgres", form.Database);

        form.Driver = DatabaseDriver.MySql;

        Assert.Equal("3306", form.Port);
        Assert.Equal("mysql", form.Database);
    }

    [Fact]
    public void 本番タグを選ぶと読み取り専用が既定で入る()
    {
        var form = new ConnectionFormViewModel
        {
            Environment = EnvironmentChoiceViewModel.For(EnvironmentTag.Local)
        };

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
        var form = new ConnectionFormViewModel { Driver = DatabaseDriver.Sqlite };

        Assert.False(form.SupportsNetworkAddress);
        Assert.False(form.RequiresPassword);
        Assert.Equal("ファイル", form.HostLabel);
    }

    [Fact]
    public void 入力から接続URLが組み立てられる()
    {
        var form = NewForm();

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
        var form = new ConnectionFormViewModel { Driver = DatabaseDriver.PostgreSql };

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
        var form = new ConnectionFormViewModel();

        form.Load(profile);

        Assert.Equal(profile.Name, form.Name);
        Assert.Equal(profile.Target.Address.Host, form.Host);
        Assert.Equal(profile.Environment, form.Environment.Tag);
        Assert.True(ConnectionValidator.Validate(form.ToDraft()).IsValid);
    }

    private static ConnectionFormViewModel NewForm()
    {
        var form = new ConnectionFormViewModel { Driver = DatabaseDriver.PostgreSql };
        form.Host = "10.2.0.14";
        form.Database = "sales_db";
        form.User = "analyst_ro";
        form.Tls = TlsChoice.For(TlsMode.VerifyFull);
        return form;
    }
}
