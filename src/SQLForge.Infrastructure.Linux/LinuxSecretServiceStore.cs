using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Security;

namespace SQLForge.Infrastructure.Linux;

/// <summary>
/// Linux での資格情報の預け先。<c>secret-tool</c>（libsecret）ごしに
/// Secret Service (org.freedesktop.secrets) へ預ける。実体は GNOME キーリングや
/// KWallet で、中身の暗号化はそちらの担当。
///
/// <c>seahorse</c> などには <c>service=sqlforge, account=&lt;接続の Id&gt;</c> の
/// 属性を持つ項目として並ぶ。
/// </summary>
public sealed class LinuxSecretServiceStore : CommandLineSecretStore
{
    private const string SecretTool = "secret-tool";

    public override PlatformKind Kind => PlatformKind.Linux;

    /// <summary>
    /// 道具（secret-tool）とセッション バスの両方が要る。
    /// サーバー上の CI やコンテナのようにどちらも無い環境では、
    /// キーリング無しと見なしてパスワードの都度入力へ落とす。
    /// </summary>
    public override bool IsAvailable =>
        OperatingSystem.IsLinux() && ExistsOnPath(SecretTool) && HasSessionBus();

    protected override string KeyringName => "キーリング";

    protected override async Task SaveCoreAsync(string key, string secret, CancellationToken cancellationToken)
    {
        // secret-tool はパスワードを標準入力から受け取る（引数に出さずに済む）。
        var result = await RunAsync(
            SecretTool,
            ["store", "--label", $"SQLForge: {key}", "service", ServiceName, "account", key],
            secret,
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw Failed($"{key} をキーリングへ預けられません。", result);
        }
    }

    protected override async Task<string?> ReadCoreAsync(string key, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            SecretTool,
            ["lookup", "service", ServiceName, "account", key],
            input: null,
            cancellationToken).ConfigureAwait(false);

        // 預けたものが無いときは、終了コードで知らせる版と何も出さない版がある。
        if (!result.Succeeded)
        {
            return result.StandardError.Length == 0
                ? null
                : throw Failed($"{key} をキーリングから読めません。", result);
        }

        return result.StandardOutput.Length == 0 ? null : result.StandardOutput.TrimEnd('\n');
    }

    protected override async Task DeleteCoreAsync(string key, CancellationToken cancellationToken)
    {
        // clear は消すものが無くても成功する。
        var result = await RunAsync(
            SecretTool,
            ["clear", "service", ServiceName, "account", key],
            input: null,
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded && result.StandardError.Length > 0)
        {
            throw Failed($"{key} をキーリングから消せません。", result);
        }
    }

    private static bool HasSessionBus()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
        {
            return true;
        }

        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

        return !string.IsNullOrEmpty(runtimeDirectory) && File.Exists(Path.Combine(runtimeDirectory, "bus"));
    }

    private static InvalidOperationException Failed(string message, CommandResult result) =>
        new($"{message} secret-tool: {result.StandardError.Trim()}");
}
