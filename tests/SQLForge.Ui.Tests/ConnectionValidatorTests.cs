using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 「SSH トンネル」「詳細設定」タブの検証。
/// 使わない設定の書きかけで接続できなくなる、という取りこぼしをここで押さえる。
/// </summary>
public class ConnectionValidatorTests
{
    [Fact]
    public void トンネルを使わないなら踏み台の書きかけは見ない()
    {
        // 一度書いてから「使わない」に戻した接続を、書きかけのまま弾かない。
        var draft = Draft() with { Tunnel = new SshTunnelSettings { Host = "bastion.internal" } };

        Assert.True(ConnectionValidator.Validate(draft).IsValid);
    }

    [Fact]
    public void トンネルを使うなら踏み台のホストと利用者名が要る()
    {
        var draft = Draft() with { Tunnel = new SshTunnelSettings { IsEnabled = true } };

        var validation = ConnectionValidator.Validate(draft);

        Assert.False(validation.IsValid);
        Assert.NotNull(validation[ConnectionValidator.SshHostField]);
        Assert.NotNull(validation[ConnectionValidator.SshUserField]);
    }

    [Fact]
    public void 秘密鍵を選んだらファイルの指定が要る()
    {
        var draft = Draft() with
        {
            Tunnel = Tunnel() with { Authentication = SshAuthenticationMethod.PrivateKey }
        };

        Assert.NotNull(ConnectionValidator.Validate(draft)[ConnectionValidator.SshKeyField]);
    }

    [Fact]
    public void 手元のポートは自動か範囲内でなければ弾く()
    {
        var automatic = Draft() with { Tunnel = Tunnel() with { LocalPort = 0 } };
        var outOfRange = Draft() with { Tunnel = Tunnel() with { LocalPort = 70000 } };

        Assert.True(ConnectionValidator.Validate(automatic).IsValid);
        Assert.NotNull(ConnectionValidator.Validate(outOfRange)[ConnectionValidator.SshLocalPortField]);
    }

    [Fact]
    public void 踏み台のポートも範囲で見る()
    {
        var draft = Draft() with { Tunnel = Tunnel() with { Port = 0 } };

        Assert.NotNull(ConnectionValidator.Validate(draft)[ConnectionValidator.SshPortField]);
    }

    [Theory]
    [InlineData(511)]
    [InlineData(32769)]
    public void パケットサイズは範囲外を弾く(int packetSize)
    {
        var draft = Draft() with { Advanced = new AdvancedConnectionSettings { PacketSize = packetSize } };

        Assert.NotNull(ConnectionValidator.Validate(draft)[ConnectionValidator.PacketSizeField]);
    }

    [Fact]
    public void 待ち時間は負の値を弾く()
    {
        var draft = Draft() with
        {
            Advanced = new AdvancedConnectionSettings { ConnectTimeoutSeconds = -1, ExecutionTimeoutSeconds = -1 }
        };

        var validation = ConnectionValidator.Validate(draft);

        Assert.NotNull(validation[ConnectionValidator.ConnectTimeoutField]);
        Assert.NotNull(validation[ConnectionValidator.ExecutionTimeoutField]);
    }

    [Fact]
    public void 実行タイムアウトの0は待ち続ける意味なので通す()
    {
        var draft = Draft() with { Advanced = new AdvancedConnectionSettings { ExecutionTimeoutSeconds = 0 } };

        Assert.True(ConnectionValidator.Validate(draft).IsValid);
    }

    [Fact]
    public void 追加の接続パラメーターはここでは見ない()
    {
        // 引用符の中の「;」まで自前で数えると、正しい書き方を弾いてしまう。
        // 読めるかどうかはドライバーが接続文字列を組む段で分かる。
        var draft = Draft() with
        {
            Advanced = new AdvancedConnectionSettings { AdditionalParameters = "これは=でたらめ;" }
        };

        Assert.True(ConnectionValidator.Validate(draft).IsValid);
    }

    private static SshTunnelSettings Tunnel() => new()
    {
        IsEnabled = true,
        Host = "bastion.internal",
        UserName = "alice"
    };

    private static ConnectionDraft Draft() => new()
    {
        Id = ConnectionProfileId.New(),
        Name = "sqlforge-test",
        Environment = EnvironmentTag.Local,
        Driver = DatabaseDriver.SqlServer,
        Host = "db.internal",
        Port = 1433,
        Database = "sales_db",
        UserName = "analyst_ro",
        Authentication = AuthenticationMethod.Password,
        StoreSecretInKeyring = false,
        Tls = TlsMode.Require,
        AccessMode = AccessMode.ReadWrite
    };
}
