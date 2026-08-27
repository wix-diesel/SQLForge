using System.Runtime.InteropServices;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 持ち運び用のファイル。ふだんの保存先と同じ形で書き、
/// パスワードは「書き出す」を選んだときだけ載る。
/// </summary>
public class TomlConnectionArchiveTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "sqlforge-tests", Guid.NewGuid().ToString("N"));

    public TomlConnectionArchiveTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task 書き出した接続を読み直せる()
    {
        var profile = NewProfile("prod-sales");
        var archive = new TomlConnectionArchive();

        await archive.WriteAsync(ArchivePath(), [new ArchivedConnection(profile, null)]);

        var restored = Assert.Single(await archive.ReadAsync(ArchivePath()));
        Assert.Equal(profile.Id, restored.Profile.Id);
        Assert.Equal("prod-sales", restored.Profile.Name);
        Assert.Equal(EnvironmentTag.Production, restored.Profile.Environment);
        Assert.Equal("db.internal", restored.Profile.Target.Address.Host);
        Assert.Equal("analyst_ro", restored.Profile.Credentials.UserName);
        Assert.Null(restored.Secret);
    }

    [Fact]
    public async Task パスワードを持たせた接続はパスワードごと読み直せる()
    {
        var archive = new TomlConnectionArchive();

        await archive.WriteAsync(ArchivePath(), [new ArchivedConnection(NewProfile("prod-sales"), "s3cret")]);

        var restored = Assert.Single(await archive.ReadAsync(ArchivePath()));
        Assert.Equal("s3cret", restored.Secret);
    }

    [Fact]
    public async Task パスワードを持たせなければファイルに残らない()
    {
        await new TomlConnectionArchive().WriteAsync(ArchivePath(), [new ArchivedConnection(NewProfile("prod-sales"), null)]);

        var text = await File.ReadAllTextAsync(ArchivePath());

        // 見出しの注意書きには password の語が出るので、キーの行が無いことを見る。
        Assert.DoesNotContain("password =", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 書き出したファイルは保存先としてもそのまま読める()
    {
        // 形をそろえてあるので、書き出したものを connections.toml に置いても通ること。
        var profile = NewProfile("prod-sales");
        await new TomlConnectionArchive().WriteAsync(ArchivePath(), [new ArchivedConnection(profile, "s3cret")]);

        var target = Path.Combine(_directory, "connections.toml");
        File.Copy(ArchivePath(), target);

        var restored = Assert.Single(await TomlConnectionProfileRepository.At(_directory).ListAsync());
        Assert.Equal(profile.Id, restored.Id);
    }

    [Fact]
    public async Task 書き出したファイルを別の手元へ取り込み直せる()
    {
        // 本物のファイルとキーリングごしに、書き出しから取り込みまでを通す。
        var profile = NewProfile("prod-sales");
        var source = TomlConnectionProfileRepository.At(Path.Combine(_directory, "source"));
        var sourceStore = new InMemorySecretStore();
        await source.SaveAsync(profile);
        await sourceStore.SaveAsync(SaveConnectionUseCase.SecretKeyFor(profile), "s3cret");

        var archive = new TomlConnectionArchive();
        await new ExportConnectionsUseCase(source, sourceStore, archive)
            .ExecuteAsync(ArchivePath(), [], includeCredentials: true);

        var target = TomlConnectionProfileRepository.At(Path.Combine(_directory, "target"));
        var targetStore = new InMemorySecretStore();
        var import = new ImportConnectionsUseCase(target, targetStore, archive);
        var imported = await import.ApplyAsync(await import.ReadAsync(ArchivePath()));

        Assert.Equal(1, imported);
        var restored = Assert.Single(await target.ListAsync());
        Assert.Equal("prod-sales", restored.Name);
        Assert.Equal("analyst_ro", restored.Credentials.UserName);
        Assert.Equal("s3cret", await targetStore.ReadAsync(SaveConnectionUseCase.SecretKeyFor(restored)));
    }

    [Fact]
    public async Task 読めないファイルは場所つきで失敗する()
    {
        await File.WriteAllTextAsync(ArchivePath(), "[[connection]]\nname = \"壊れている\"\n");

        var failure = await Assert.ThrowsAsync<FormatException>(() => new TomlConnectionArchive().ReadAsync(ArchivePath()));

        Assert.Contains(ArchivePath(), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 中身を書く前に本人だけが読める権限にする()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // 作ってから絞ると、その隙間（umask 次第では他人も読める権限のまま）に
        // パスワードを覗かれうる。書き込みを断られた時点でファイルがもう 0600 に
        // なっていることで、「絞ってから書く」順になっていることを確かめる。
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TomlConnectionArchive()
            .WriteAsync(ArchivePath(), [new ArchivedConnection(NewProfile("prod-sales"), "s3cret")], cancellation.Token));

        Assert.True(File.Exists(ArchivePath()), "中身より先にファイルを作っていること。");
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ArchivePath()));
        Assert.Empty(await File.ReadAllTextAsync(ArchivePath()));
    }

    [Fact]
    public async Task すでにあるファイルへ書き直しても本人だけが読める権限になる()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // 誰でも読めるファイルへ上書きするとき、中身を書いてから絞ると
        // その隙間にパスワードを覗かれうる。書く前に絞れていること。
        await File.WriteAllTextAsync(ArchivePath(), string.Empty);
        File.SetUnixFileMode(
            ArchivePath(),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        await new TomlConnectionArchive().WriteAsync(ArchivePath(), [new ArchivedConnection(NewProfile("prod-sales"), "s3cret")]);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ArchivePath()));
    }

    [Fact]
    public async Task Unixでは本人だけが読める権限で置く()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows の権限は ACL 側の話なので、ここでは何も要求しない。
            return;
        }

        await new TomlConnectionArchive().WriteAsync(ArchivePath(), [new ArchivedConnection(NewProfile("prod-sales"), "s3cret")]);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ArchivePath()));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string ArchivePath() => Path.Combine(_directory, "export.toml");

    private static ConnectionProfile NewProfile(string name) =>
        new(ConnectionProfileId.New(),
            name,
            EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db", TlsMode.Require),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: true),
            AccessMode.ReadOnly);
}
