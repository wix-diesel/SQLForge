using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.Composition;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 合成ルートの配線。差し込むものを増やしたときに、起動して初めて気づくことがないよう、
/// 実際に組み上げて確かめる（ここではファイルもキーリングも触らない）。
/// </summary>
public class AppServicesTests
{
    [Fact]
    public void 接続ダイアログを組み立てられる()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<ConnectDialogViewModel>());
    }

    [Fact]
    public void メインウィンドウの工場を組み立てられる()
    {
        // ツリーが使うユースケースとダイアログの行き先は、ここが一手に受け取る。
        // 登録を足し忘れると、接続が通ったあと（起動して初めて）に落ちる。
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<MainWindowViewModelFactory>());
    }

    [Fact]
    public void 保存済み接続はOSの設定ディレクトリのファイルに置く()
    {
        using var provider = BuildProvider();

        var repository = Assert.IsType<TomlConnectionProfileRepository>(
            provider.GetRequiredService<IConnectionProfileRepository>());

        var directory = provider.GetRequiredService<IPlatformProfile>().ProfileDirectory;
        Assert.Equal(Path.Combine(directory, "connections.toml"), repository.FilePath);
    }

    [Fact]
    public void 保存済み接続の書き出しと取り込みを組み立てられる()
    {
        // 左ペインの削除・書き出し・取り込みは、差し込み忘れると
        // 起動して右クリックした瞬間に落ちる。
        using var provider = BuildProvider();

        Assert.IsType<TomlConnectionArchive>(provider.GetRequiredService<IConnectionArchive>());
        Assert.Same(
            provider.GetRequiredService<SavedConnectionDialogService>(),
            provider.GetRequiredService<ISavedConnectionPrompt>());
        Assert.NotNull(provider.GetRequiredService<SavedConnectionsViewModel>());
    }

    [Fact]
    public void 資格情報は実行中のOSの預け先に預ける()
    {
        using var provider = BuildProvider();

        var store = provider.GetRequiredService<ISecretStore>();

        Assert.Equal(SecretStores.ForCurrentHost().DisplayName, store.DisplayName);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        AppServices.Configure(services);

        return services.BuildServiceProvider();
    }
}
