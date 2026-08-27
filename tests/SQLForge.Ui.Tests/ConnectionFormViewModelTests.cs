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

    [Fact]
    public void タブごとの入力がドラフトに載る()
    {
        var form = FilledForm();

        form.Ssh.IsEnabled = true;
        form.Ssh.Host = " bastion.internal ";
        form.Ssh.Port = "2222";
        form.Ssh.User = "alice";
        form.Ssh.Authentication = SshAuthenticationChoice.For(SshAuthenticationMethod.PrivateKey);
        form.Ssh.PrivateKeyPath = "~/.ssh/id_ed25519";
        form.Certificate.HostNameInCertificate = "db.internal";
        form.Advanced.PacketSize = "8192";
        form.Advanced.AdditionalParameters = "ApplicationIntent=ReadOnly";

        var draft = form.ToDraft();

        Assert.True(draft.Tunnel.IsEnabled);
        Assert.Equal("bastion.internal", draft.Tunnel.Host);
        Assert.Equal(2222, draft.Tunnel.Port);
        Assert.Equal(SshAuthenticationMethod.PrivateKey, draft.Tunnel.Authentication);
        Assert.Equal("db.internal", draft.Certificate.HostNameInCertificate);
        Assert.Equal(8192, draft.Advanced.PacketSize);
        Assert.Equal("ApplicationIntent=ReadOnly", draft.Advanced.AdditionalParameters);
    }

    [Fact]
    public void 保存済み接続を読み込むとタブの入力にも写る()
    {
        var form = NewForm();
        var loaded = FilledForm();
        loaded.Ssh.IsEnabled = true;
        loaded.Ssh.Host = "bastion.internal";
        loaded.Ssh.User = "alice";
        loaded.Advanced.ConnectTimeout = "30";

        form.Load(loaded.ToDraft());

        Assert.True(form.Ssh.IsEnabled);
        Assert.Equal("bastion.internal", form.Ssh.Host);
        Assert.Equal("30", form.Advanced.ConnectTimeout);

        // パスワードと同じで、預けてある秘密は入力欄へは戻さない。
        Assert.Empty(form.Ssh.Secret);
    }

    [Fact]
    public void 手元のポートは空欄なら自動になる()
    {
        var form = FilledForm();
        form.Ssh.IsEnabled = true;
        form.Ssh.LocalPort = string.Empty;

        Assert.True(form.ToDraft().Tunnel.UsesAutomaticLocalPort);
    }

    [Fact]
    public void 一般タブのTLSがTLSタブの表示に追随する()
    {
        // 要求レベルは 1 か所（「一般」タブ）でだけ決まる。
        var form = FilledForm();

        form.Tls = TlsChoice.For(TlsMode.Require);
        Assert.True(form.Certificate.TrustsServerCertificate);

        form.Tls = TlsChoice.For(TlsMode.Strict);

        Assert.False(form.Certificate.TrustsServerCertificate);
        Assert.Equal("厳密 (Strict)", form.Certificate.EncryptionName);
    }

    [Fact]
    public void 証明書を指定していても検証しない設定なら断り書きを出す()
    {
        var form = FilledForm();
        form.Tls = TlsChoice.For(TlsMode.Require);
        form.Certificate.HostNameInCertificate = "db.internal";

        Assert.True(form.Certificate.ShowsIgnoredNotice);

        form.Tls = TlsChoice.For(TlsMode.VerifyFull);

        Assert.False(form.Certificate.ShowsIgnoredNotice);
    }

    [Fact]
    public void 詳細設定は既定値へ戻せる()
    {
        var form = FilledForm();
        form.Advanced.PacketSize = "8192";

        Assert.False(form.Advanced.IsDefault);

        form.Advanced.ResetCommand.Execute(null);

        Assert.True(form.Advanced.IsDefault);
        Assert.Equal(AdvancedConnectionSettings.DefaultPacketSize, form.ToDraft().Advanced.PacketSize);
    }

    [Fact]
    public void 検証の結果はタブごとの入力欄にも配る()
    {
        var form = FilledForm();
        form.Ssh.IsEnabled = true;

        form.Validation = ConnectionValidator.Validate(form.ToDraft());

        Assert.True(form.Ssh.HasHostError);
        Assert.True(form.Ssh.HasUserError);
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
