using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 保存済み接続の削除・書き出し・取り込み。SSMS の「登録済みサーバー」と同じ扱いで、
/// ユーザー名とパスワードを含めるかどうかは書き出すときに選ぶ。
/// </summary>
public class SavedConnectionUseCaseTests
{
    [Fact]
    public async Task 削除は接続と預けてある資格情報の両方を消す()
    {
        var profile = Profile("prod-sales");
        var repository = InMemoryConnectionProfileRepository.With([profile]);
        var store = new InMemorySecretStore();
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(profile), "s3cret");

        await new DeleteConnectionUseCase(repository, store).ExecuteAsync(profile.Id);

        Assert.Empty(await repository.ListAsync());
        Assert.Null(await store.ReadAsync(SaveConnectionUseCase.SecretKeyFor(profile)));
    }

    [Fact]
    public async Task 書き出しの既定はユーザー名もパスワードも含めない()
    {
        var profile = Profile("prod-sales");
        var (export, _, archive, store) = Setup([profile]);
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(profile), "s3cret");

        var count = await export.ExecuteAsync("export.toml", [], includeCredentials: false);

        Assert.Equal(1, count);
        var written = Assert.Single(archive.Written);
        Assert.Empty(written.Profile.Credentials.UserName);
        Assert.Null(written.Secret);
        // 書き出さないのは資格情報だけで、繋ぎ先はそのまま持っていく。
        Assert.Equal("db.internal", written.Profile.Target.Address.Host);
        Assert.Equal(profile.Id, written.Profile.Id);
    }

    [Fact]
    public async Task 含めると預けてあるパスワードごと書き出す()
    {
        var profile = Profile("prod-sales");
        var (export, _, archive, store) = Setup([profile]);
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(profile), "s3cret");

        await export.ExecuteAsync("export.toml", [], includeCredentials: true);

        var written = Assert.Single(archive.Written);
        Assert.Equal("analyst_ro", written.Profile.Credentials.UserName);
        Assert.Equal("s3cret", written.Secret);
    }

    [Fact]
    public async Task 選んだ接続だけを書き出せる()
    {
        var target = Profile("prod-sales");
        var (export, _, archive, _) = Setup([target, Profile("local-dev", EnvironmentTag.Local)]);

        var count = await export.ExecuteAsync("export.toml", [target.Id], includeCredentials: false);

        Assert.Equal(1, count);
        Assert.Equal("prod-sales", Assert.Single(archive.Written).Profile.Name);
    }

    [Fact]
    public async Task 手元に無い接続はそのまま取り込める()
    {
        var (_, import, archive, _) = Setup([]);
        archive.Stored = [new ArchivedConnection(Profile("prod-sales"), null)];

        var candidates = await import.ReadAsync("export.toml");

        var candidate = Assert.Single(candidates);
        Assert.False(candidate.ConflictsWithExisting);
        Assert.Equal(1, await import.ApplyAsync(candidates));
    }

    [Fact]
    public async Task 同じIdの接続は当たりとして返す()
    {
        var existing = Profile("prod-sales");
        var (_, import, archive, _) = Setup([existing]);
        archive.Stored =
        [
            new ArchivedConnection(
                new ConnectionProfile(
                    existing.Id, "prod-sales-renamed", existing.Environment, existing.Target,
                    existing.Credentials, existing.AccessMode),
                null)
        ];

        var candidate = Assert.Single(await import.ReadAsync("export.toml"));

        Assert.Equal(existing.Id, candidate.Existing?.Id);
    }

    [Fact]
    public async Task 同じ環境の同じ名前も当たりとして返す()
    {
        // 別の環境から持ってきたファイルは Id が違うので、SSMS と同じく名前で見る。
        var existing = Profile("prod-sales");
        var (_, import, archive, _) = Setup([existing]);
        archive.Stored = [new ArchivedConnection(Profile("PROD-SALES"), null)];

        var candidate = Assert.Single(await import.ReadAsync("export.toml"));

        Assert.Equal(existing.Id, candidate.Existing?.Id);
    }

    [Fact]
    public async Task 名前が同じでも環境タグが違えば別の接続として足す()
    {
        var (_, import, archive, _) = Setup([Profile("sales")]);
        archive.Stored = [new ArchivedConnection(Profile("sales", EnvironmentTag.Local), null)];

        var candidate = Assert.Single(await import.ReadAsync("export.toml"));

        Assert.False(candidate.ConflictsWithExisting);
    }

    [Fact]
    public async Task 名前で当たったものを置き換えると古いほうが消える()
    {
        var existing = Profile("prod-sales");
        var repository = InMemoryConnectionProfileRepository.With([existing]);
        var store = new InMemorySecretStore();
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(existing), "old");
        var archive = new FakeConnectionArchive
        {
            Stored = [new ArchivedConnection(Profile("prod-sales"), "new")]
        };
        var import = new ImportConnectionsUseCase(repository, store, archive);

        await import.ApplyAsync(await import.ReadAsync("export.toml"));

        var restored = Assert.Single(await repository.ListAsync());
        Assert.NotEqual(existing.Id, restored.Id);
        Assert.Null(await store.ReadAsync(SaveConnectionUseCase.SecretKeyFor(existing)));
        Assert.Equal("new", await store.ReadAsync(SaveConnectionUseCase.SecretKeyFor(restored)));
    }

    [Fact]
    public async Task 飛ばした接続は手元のまま残る()
    {
        var existing = Profile("prod-sales");
        var repository = InMemoryConnectionProfileRepository.With([existing]);
        var archive = new FakeConnectionArchive
        {
            Stored = [new ArchivedConnection(Profile("prod-sales"), null), new ArchivedConnection(Profile("local-dev", EnvironmentTag.Local), null)]
        };
        var import = new ImportConnectionsUseCase(repository, new InMemorySecretStore(), archive);
        var candidates = await import.ReadAsync("export.toml");

        var count = await import.ApplyAsync(candidates.Where(candidate => !candidate.ConflictsWithExisting));

        Assert.Equal(1, count);
        var names = (await repository.ListAsync()).Select(profile => profile.Name).Order();
        Assert.Equal(["local-dev", "prod-sales"], names);
        Assert.Equal(existing.Id, (await repository.FindAsync(existing.Id))?.Id);
    }

    [Fact]
    public async Task パスワードの入っていないファイルはキーリングを触らない()
    {
        // 「含めない」で書き出したファイルの取り込みで、預けてあるパスワードを消してしまわないこと。
        var existing = Profile("prod-sales");
        var repository = InMemoryConnectionProfileRepository.With([existing]);
        var store = new InMemorySecretStore();
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(existing), "kept");
        var archive = new FakeConnectionArchive
        {
            Stored =
            [
                new ArchivedConnection(
                    new ConnectionProfile(
                        existing.Id, existing.Name, existing.Environment, existing.Target,
                        new ConnectionCredentials(string.Empty, AuthenticationMethod.Password, true),
                        existing.AccessMode),
                    null)
            ]
        };
        var import = new ImportConnectionsUseCase(repository, store, archive);

        await import.ApplyAsync(await import.ReadAsync("export.toml"));

        Assert.Equal("kept", await store.ReadAsync(SaveConnectionUseCase.SecretKeyFor(existing)));
    }

    private static (ExportConnectionsUseCase Export, ImportConnectionsUseCase Import, FakeConnectionArchive Archive, InMemorySecretStore Store)
        Setup(IReadOnlyList<ConnectionProfile> profiles)
    {
        var repository = InMemoryConnectionProfileRepository.With(profiles);
        var store = new InMemorySecretStore();
        var archive = new FakeConnectionArchive();

        return (new ExportConnectionsUseCase(repository, store, archive),
            new ImportConnectionsUseCase(repository, store, archive),
            archive,
            store);
    }

    private static ConnectionProfile Profile(string name, EnvironmentTag? environment = null) =>
        new(ConnectionProfileId.New(),
            name,
            environment ?? EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db", TlsMode.Require),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: true),
            AccessMode.ReadOnly);
}
