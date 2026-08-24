using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.Composition;
using SQLForge.Ui.ViewModels;
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
    public void 保存済み接続はOSの設定ディレクトリのファイルに置く()
    {
        using var provider = BuildProvider();

        var repository = Assert.IsType<TomlConnectionProfileRepository>(
            provider.GetRequiredService<IConnectionProfileRepository>());

        var directory = provider.GetRequiredService<IPlatformProfile>().ProfileDirectory;
        Assert.Equal(Path.Combine(directory, "connections.toml"), repository.FilePath);
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
