using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>接続時にどのパスワードを使うか。</summary>
public class ConnectionSecretResolverTests
{
    [Fact]
    public async Task 入力欄のパスワードを優先する()
    {
        var (resolver, store, profile) = Setup();
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(profile), "keyring");

        var request = await resolver.ResolveAsync(profile, new ConnectionSecrets("typed"));

        Assert.Equal("typed", request.Secret);
    }

    [Fact]
    public async Task 入力欄が空ならキーリングから読む()
    {
        // 保存済み接続を開いた直後はパスワード欄が空なので、預けてあるものを使う。
        var (resolver, store, profile) = Setup();
        await store.SaveAsync(SaveConnectionUseCase.SecretKeyFor(profile), "keyring");

        var request = await resolver.ResolveAsync(profile, new ConnectionSecrets(string.Empty));

        Assert.Equal("keyring", request.Secret);
    }

    [Fact]
    public async Task パスワード認証でなければ資格情報を持ち回らない()
    {
        var (resolver, _, profile) = Setup();
        var integrated = new ConnectionProfile(
            profile.Id,
            profile.Name,
            profile.Environment,
            profile.Target,
            new ConnectionCredentials(string.Empty, AuthenticationMethod.Integrated, storeSecretInKeyring: true),
            profile.AccessMode);

        var request = await resolver.ResolveAsync(integrated, new ConnectionSecrets("typed"));

        Assert.Null(request.Secret);
    }

    private static (ConnectionSecretResolver Resolver, InMemorySecretStore Store, ConnectionProfile Profile) Setup()
    {
        var store = new InMemorySecretStore();

        return (new ConnectionSecretResolver(store), store, SeedConnections.Create().First());
    }
}
