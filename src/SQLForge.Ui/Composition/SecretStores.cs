using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Linux;
using SQLForge.Infrastructure.MacOs;
using SQLForge.Infrastructure.Security;
using SQLForge.Infrastructure.Windows;

namespace SQLForge.Ui.Composition;

/// <summary>
/// OS ごとの資格情報の預け先を知ってよいのは合成ルートだけ。
/// OS を増やすときに触るのは、新しい SQLForge.Infrastructure.&lt;OS&gt; と、この並びの 1 行。
/// </summary>
public static class SecretStores
{
    /// <summary>預け先そのものは状態を持たないので、台帳ごと使い回す。</summary>
    private static readonly Lazy<SecretStoreRegistry> Registry = new(() => new SecretStoreRegistry(
    [
        new LinuxSecretServiceStore(),
        new WindowsCredentialStore(),
        new MacOsKeychainStore()
    ]));

    /// <summary>実行中の OS の預け先。使える預け先が無ければ「利用できない」預け先になる。</summary>
    public static ISecretStore ForCurrentHost() => Registry.Value.ForCurrentHost();
}
