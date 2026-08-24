using System.Runtime.InteropServices;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 保存済み接続の永続化。ファイルに残るのは「どこへ誰として繋ぐか」までで、
/// パスワードはキーリング側の担当なのでここには一切書かない。
/// </summary>
public class TomlConnectionProfileRepositoryTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "sqlforge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ファイルが無ければ一覧は空()
    {
        var repository = NewRepository();

        Assert.Empty(await repository.ListAsync());
    }

    [Fact]
    public async Task 保存した接続は別のインスタンスから読み直せる()
    {
        var profile = NewProfile("prod-sales");
        await NewRepository().SaveAsync(profile);

        var restored = Assert.Single(await NewRepository().ListAsync());

        Assert.Equal(profile.Id, restored.Id);
        Assert.Equal("prod-sales", restored.Name);
        Assert.Equal(EnvironmentTag.Production, restored.Environment);
        Assert.Equal(DatabaseDriver.SqlServer, restored.Target.Driver);
        Assert.Equal("db.internal", restored.Target.Address.Host);
        Assert.Equal(1433, restored.Target.Address.Port);
        Assert.Equal("sales_db", restored.Target.Database);
        Assert.Equal(TlsMode.Require, restored.Target.Tls);
        Assert.Equal("analyst_ro", restored.Credentials.UserName);
        Assert.Equal(AuthenticationMethod.Password, restored.Credentials.Method);
        Assert.True(restored.Credentials.StoreSecretInKeyring);
        Assert.Equal(AccessMode.ReadOnly, restored.AccessMode);
    }

    [Fact]
    public async Task 同じIdの保存は上書きになる()
    {
        var profile = NewProfile("prod-sales");
        var repository = NewRepository();
        await repository.SaveAsync(profile);

        await repository.SaveAsync(new ConnectionProfile(
            profile.Id, "prod-sales-2", profile.Environment, profile.Target, profile.Credentials, profile.AccessMode));

        var restored = Assert.Single(await NewRepository().ListAsync());
        Assert.Equal("prod-sales-2", restored.Name);
    }

    [Fact]
    public async Task 削除した接続はファイルから消える()
    {
        var kept = NewProfile("local-dev");
        var removed = NewProfile("prod-sales");
        var repository = NewRepository();
        await repository.SaveAsync(kept);
        await repository.SaveAsync(removed);

        await repository.DeleteAsync(removed.Id);

        var restored = Assert.Single(await NewRepository().ListAsync());
        Assert.Equal(kept.Id, restored.Id);
        Assert.Null(await NewRepository().FindAsync(removed.Id));
    }

    [Fact]
    public async Task ファイルにパスワードは書かれない()
    {
        await NewRepository().SaveAsync(NewProfile("prod-sales"));

        var text = await File.ReadAllTextAsync(NewRepository().FilePath);

        Assert.Contains("analyst_ro", text, StringComparison.Ordinal);
        Assert.DoesNotContain("password =", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret =", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 名前に記号が入っていても読み直せる()
    {
        // TOML の文字列として書き出すので、引用符や改行が混ざっても壊れないこと。
        var profile = new ConnectionProfile(
            ConnectionProfileId.New(),
            """本番 "東京" \ 1号機""",
            EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db"),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, true),
            AccessMode.ReadOnly);
        await NewRepository().SaveAsync(profile);

        var restored = Assert.Single(await NewRepository().ListAsync());

        Assert.Equal(profile.Name, restored.Name);
    }

    [Fact]
    public async Task OS統合認証の接続は利用者名を持たずに読み直せる()
    {
        // 利用者名が空でも TOML の往復で壊れないこと（統合認証では OS が名乗る）。
        var profile = new ConnectionProfile(
            ConnectionProfileId.New(),
            "prod-sales-integrated",
            EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db", TlsMode.Require),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Integrated, storeSecretInKeyring: true),
            AccessMode.ReadOnly);
        await NewRepository().SaveAsync(profile);

        var restored = Assert.Single(await NewRepository().ListAsync());

        Assert.Equal(AuthenticationMethod.Integrated, restored.Credentials.Method);
        Assert.Empty(restored.Credentials.UserName);
        Assert.False(restored.Credentials.StoreSecretInKeyring);
    }

    [Fact]
    public async Task 壊れたファイルは理由付きで失敗する()
    {
        // 黙って捨てると利用者の接続情報が消えたように見えるので、読めないことを伝える。
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(NewRepository().FilePath, "[[connection]]\nname = \"壊れている\"\n");

        var failure = await Assert.ThrowsAsync<FormatException>(() => NewRepository().ListAsync());

        Assert.Contains("connections.toml", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unixでは本人だけが読める権限で置く()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows の権限は ACL 側の話なので、ここでは何も要求しない。
            return;
        }

        await NewRepository().SaveAsync(NewProfile("prod-sales"));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(NewRepository().FilePath));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private TomlConnectionProfileRepository NewRepository() => TomlConnectionProfileRepository.At(_directory);

    private static ConnectionProfile NewProfile(string name) =>
        new(ConnectionProfileId.New(),
            name,
            EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db", TlsMode.Require),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: true),
            AccessMode.ReadOnly);
}
