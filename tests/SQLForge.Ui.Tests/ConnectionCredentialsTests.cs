using SQLForge.Domain.Connections;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 「誰として繋ぐか」の規則。OS 統合認証では名乗る相手を OS が決めるので、
/// 入力欄に残っていた利用者名を持ち越さないことをここで固定する。
/// </summary>
public class ConnectionCredentialsTests
{
    [Fact]
    public void OS統合認証では利用者名を持たない()
    {
        // パスワード認証から切り替えたときに、使われない利用者名が
        // 保存内容と接続 URL に残ってしまうのを防ぐ。
        var credentials = new ConnectionCredentials("analyst_ro", AuthenticationMethod.Integrated, storeSecretInKeyring: true);

        Assert.Empty(credentials.UserName);
    }

    [Fact]
    public void OS統合認証ではパスワードを預からない()
    {
        var credentials = new ConnectionCredentials("analyst_ro", AuthenticationMethod.Integrated, storeSecretInKeyring: true);

        Assert.False(credentials.RequiresSecret);
        Assert.False(credentials.StoreSecretInKeyring);
    }

    [Fact]
    public void パスワード認証では利用者名を前後の空白だけ落として持つ()
    {
        var credentials = new ConnectionCredentials("  analyst_ro  ", AuthenticationMethod.Password, storeSecretInKeyring: true);

        Assert.Equal("analyst_ro", credentials.UserName);
        Assert.True(credentials.RequiresSecret);
        Assert.True(credentials.StoreSecretInKeyring);
    }
}
