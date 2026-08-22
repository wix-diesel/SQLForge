using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>資格情報を預ける／消すの切り分け。</summary>
public class SaveConnectionUseCaseTests
{
    [Fact]
    public async Task パスワードを入力して保存するとキーリングに預ける()
    {
        var (useCase, store, draft) = Setup();

        await useCase.ExecuteAsync(draft, "s3cret");

        Assert.Equal("s3cret", await store.ReadAsync(KeyFor(draft)));
    }

    [Fact]
    public async Task 保存トグルを外すと預けてある資格情報を消す()
    {
        var (useCase, store, draft) = Setup();
        await useCase.ExecuteAsync(draft, "s3cret");

        await useCase.ExecuteAsync(draft with { StoreSecretInKeyring = false }, "s3cret");

        Assert.Null(await store.ReadAsync(KeyFor(draft)));
    }

    [Fact]
    public async Task パスワード認証をやめると預けてある資格情報を消す()
    {
        var (useCase, store, draft) = Setup();
        await useCase.ExecuteAsync(draft, "s3cret");

        await useCase.ExecuteAsync(draft with { Authentication = AuthenticationMethod.Integrated }, null);

        Assert.Null(await store.ReadAsync(KeyFor(draft)));
    }

    [Fact]
    public async Task パスワード欄が空のままの保存では既存の資格情報を残す()
    {
        // 保存済み接続を開くとパスワード欄は空になるので、空を削除扱いにしてはいけない。
        var (useCase, store, draft) = Setup();
        await useCase.ExecuteAsync(draft, "s3cret");

        await useCase.ExecuteAsync(draft with { Database = "other_db" }, string.Empty);

        Assert.Equal("s3cret", await store.ReadAsync(KeyFor(draft)));
    }

    private static string KeyFor(ConnectionDraft draft) => SaveConnectionUseCase.SecretKeyFor(draft.ToProfile());

    private static (SaveConnectionUseCase UseCase, ISecretStore Store, ConnectionDraft Draft) Setup()
    {
        var store = new InMemorySecretStore();
        var useCase = new SaveConnectionUseCase(new InMemoryConnectionProfileRepository(), store);

        var draft = ConnectionDraft.FromProfile(SeedConnections.Create().First());

        return (useCase, store, draft);
    }
}
