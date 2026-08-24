using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Linux;
using SQLForge.Infrastructure.MacOs;
using SQLForge.Infrastructure.Platform;
using SQLForge.Infrastructure.Security;
using SQLForge.Infrastructure.Windows;
using SQLForge.Ui.Composition;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 資格情報の預け先。実装は OS ごとの別プロジェクトにあるが、選び分けは台帳ごしなので
/// どの OS の上からでも 3 つとも確かめられる（CI は Linux でしか回らないため）。
/// </summary>
public class SecretStoreTests
{
    private static readonly SecretStoreRegistry Registry = new(
    [
        new LinuxSecretServiceStore(),
        new WindowsCredentialStore(),
        new MacOsKeychainStore()
    ]);

    [Theory]
    [InlineData(PlatformKind.Linux, typeof(LinuxSecretServiceStore))]
    [InlineData(PlatformKind.Windows, typeof(WindowsCredentialStore))]
    [InlineData(PlatformKind.MacOs, typeof(MacOsKeychainStore))]
    public void OSごとの預け先を引き分ける(PlatformKind kind, Type expected)
    {
        var store = Registry.ForHost(kind);

        Assert.IsType(expected, store);
    }

    [Fact]
    public void 預け先の無いOSでは利用できないと名乗る()
    {
        // 起動できなくなるより、「キーリングを利用できません」と出して都度入力にしたほうがよい。
        var store = Registry.ForHost(PlatformKind.Unknown);

        Assert.False(store.IsAvailable);
        Assert.Contains("利用できません", store.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 利用できない預け先は何も返さない()
    {
        var store = Registry.ForHost(PlatformKind.Unknown);

        await store.SaveAsync("sqlforge-test:unknown", "s3cret");

        Assert.Null(await store.ReadAsync("sqlforge-test:unknown"));
    }

    [Fact]
    public void 合成ルートは実行中のOSの預け先を選ぶ()
    {
        var store = SecretStores.ForCurrentHost();

        // 使える環境なら OS の名前（資格情報マネージャー等）を、使えない環境ならその旨を名乗る。
        Assert.False(string.IsNullOrWhiteSpace(store.DisplayName));
    }

    [Fact]
    public async Task 実行中のOSのキーリングに預けて読み戻して消せる()
    {
        var store = SecretStores.ForCurrentHost();
        if (!store.IsAvailable)
        {
            // キーリングの無い環境（CI のコンテナなど）では、都度入力へ落ちることだけを確かめる。
            Assert.Null(await store.ReadAsync("sqlforge-test:absent"));
            return;
        }

        var key = $"sqlforge-test:{Guid.NewGuid():N}";
        try
        {
            await store.SaveAsync(key, "s3cret-パスワード");
            Assert.Equal("s3cret-パスワード", await store.ReadAsync(key));

            await store.SaveAsync(key, "s3cret-2");
            Assert.Equal("s3cret-2", await store.ReadAsync(key));
        }
        finally
        {
            await store.DeleteAsync(key);
        }

        Assert.Null(await store.ReadAsync(key));
    }

    [Fact]
    public async Task 無いキーを消しても失敗しない()
    {
        var store = SecretStores.ForCurrentHost();

        await store.DeleteAsync($"sqlforge-test:{Guid.NewGuid():N}");
    }
}
