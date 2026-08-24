using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Security;

namespace SQLForge.Infrastructure.MacOs;

/// <summary>
/// macOS での資格情報の預け先。OS 付属の <c>security</c> ごしに、
/// ログイン キーチェーンへ「汎用パスワード」として 1 件ずつ預ける。
/// 中身の暗号化と、どのアプリに渡すかの判断はキーチェーンの担当。
///
/// 「キーチェーンアクセス」には <c>sqlforge</c> という項目名で並ぶ。
/// </summary>
public sealed class MacOsKeychainStore : CommandLineSecretStore
{
    private const string Security = "/usr/bin/security";

    /// <summary>キーチェーンに項目が無いときの終了コード（errSecItemNotFound）。</summary>
    private const int ItemNotFound = 44;

    public override PlatformKind Kind => PlatformKind.MacOs;

    public override bool IsAvailable => OperatingSystem.IsMacOS() && File.Exists(Security);

    protected override string KeyringName => "キーチェーン";

    protected override async Task SaveCoreAsync(string key, string secret, CancellationToken cancellationToken)
    {
        // -U は同じ項目があれば上書きする指定。
        // パスワードを引数で渡すのは security の作りに合わせたもので、
        // 見えるのは同じ利用者のプロセスだけ（キーチェーンを開ける相手と同じ範囲）。
        var result = await RunAsync(
            Security,
            ["add-generic-password", "-a", key, "-s", ServiceName, "-w", secret, "-U"],
            input: null,
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw Failed($"{key} をキーチェーンへ預けられません。", result);
        }
    }

    protected override async Task<string?> ReadCoreAsync(string key, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            Security,
            ["find-generic-password", "-a", key, "-s", ServiceName, "-w"],
            input: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == ItemNotFound)
        {
            return null;
        }

        if (!result.Succeeded)
        {
            throw Failed($"{key} をキーチェーンから読めません。", result);
        }

        // security は読み出した値の後ろに改行を 1 つ足して出す。
        return result.StandardOutput.TrimEnd('\n');
    }

    protected override async Task DeleteCoreAsync(string key, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            Security,
            ["delete-generic-password", "-a", key, "-s", ServiceName],
            input: null,
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded && result.ExitCode != ItemNotFound)
        {
            throw Failed($"{key} をキーチェーンから消せません。", result);
        }
    }

    private static InvalidOperationException Failed(string message, CommandResult result) =>
        new($"{message} security: {result.StandardError.Trim()}");
}
