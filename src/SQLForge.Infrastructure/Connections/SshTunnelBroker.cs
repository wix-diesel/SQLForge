using Renci.SshNet;
using Renci.SshNet.Common;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// SSH の踏み台ごしに手元のポートを繋ぐ実装（<see cref="ISshTunnelBroker"/>）。
///
/// 待ち受けは 127.0.0.1 にだけ開く。0.0.0.0 に開くと、同じ網にいる誰でも
/// この機械ごしに踏み台の向こうへ入れてしまうため。
///
/// 既知ホストの台帳（known_hosts）は持たない。持たない以上「知っている鍵か」は
/// 判断できないので、繋いだうえでホスト鍵の指紋を接続テストの結果に出し、
/// 利用者が見比べられるようにしてある。
/// </summary>
public sealed class SshTunnelBroker : ISshTunnelBroker
{
    private const string LocalHost = "127.0.0.1";

    public async Task<ISshTunnel> OpenAsync(SshTunnelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        SshClient? client = null;
        ForwardedPortLocal? forwarded = null;

        try
        {
            var fingerprint = string.Empty;
            client = new SshClient(CreateConnectionInfo(request));
            client.HostKeyReceived += (_, host) =>
            {
                fingerprint = host.FingerPrintSHA256;
                host.CanTrust = true;
            };

            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            forwarded = StartForwarding(client, request);

            return new SshTunnel(
                client,
                forwarded,
                new ServerAddress(LocalHost, (int)forwarded.BoundPort),
                Describe(request.Settings, fingerprint));
        }
        catch (Exception exception) when (exception is OperationCanceledException or SshTunnelException)
        {
            Close(client, forwarded);
            throw;
        }
        catch (Exception exception)
        {
            Close(client, forwarded);
            throw new SshTunnelException(
                $"SSH トンネルを開けません（{request.Settings.Summary}）: {Explain(exception)}",
                exception);
        }
    }

    /// <summary>
    /// 手元の待ち受け口を開き、踏み台から繋ぎ先へ流し始める。
    /// 手元のポートに 0 を渡すと、空いているポートを OS が選ぶ。
    /// </summary>
    private static ForwardedPortLocal StartForwarding(SshClient client, SshTunnelRequest request)
    {
        var forwarded = new ForwardedPortLocal(
            LocalHost,
            (uint)request.Settings.LocalPort,
            request.Destination.Host,
            (uint)request.Destination.Port);

        client.AddForwardedPort(forwarded);
        forwarded.Start();

        return forwarded;
    }

    private static ConnectionInfo CreateConnectionInfo(SshTunnelRequest request)
    {
        var settings = request.Settings;

        ConnectionInfo info = settings.Authentication == SshAuthenticationMethod.PrivateKey
            ? new PrivateKeyConnectionInfo(
                settings.Host,
                settings.Port,
                settings.UserName,
                ReadPrivateKey(settings.PrivateKeyPath, request.Secret))
            : new PasswordConnectionInfo(settings.Host, settings.Port, settings.UserName, RequireSecret(request));

        info.Timeout = request.Timeout;

        return info;
    }

    /// <summary>
    /// パスワード認証なのに手元にパスワードが無い状態。空のまま踏み台を叩いても
    /// 断られるだけなので、何が足りないのかを先に言う。
    /// </summary>
    private static string RequireSecret(SshTunnelRequest request) =>
        string.IsNullOrEmpty(request.Secret)
            ? throw new SshTunnelException($"踏み台（{request.Settings.Summary}）のパスワードが要ります。")
            : request.Secret;

    private static PrivateKeyFile ReadPrivateKey(string path, string? passphrase)
    {
        var file = ExpandHome(path);

        if (!File.Exists(file))
        {
            throw new SshTunnelException($"秘密鍵のファイルがありません: {file}");
        }

        return string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(file) : new PrivateKeyFile(file, passphrase);
    }

    /// <summary>先頭の <c>~</c> をホームとして扱う。鍵の置き場所はほぼこの書き方で渡ってくる。</summary>
    private static string ExpandHome(string path) =>
        path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;

    private static string Describe(SshTunnelSettings settings, string fingerprint) =>
        fingerprint.Length > 0
            ? $"SSH {settings.Summary} · ホスト鍵 SHA256:{fingerprint}"
            : $"SSH {settings.Summary}";

    /// <summary>そのままでは何を直せばよいか分からない失敗にだけ、言い換えを足す。</summary>
    private static string Explain(Exception exception) => exception switch
    {
        SshAuthenticationException => $"踏み台に名乗れませんでした（{exception.Message}）",
        SshPassPhraseNullOrEmptyException => "秘密鍵にパスフレーズが掛かっています。パスフレーズを入力してください。",
        SshOperationTimeoutException => "踏み台からの応答がありませんでした。",
        _ => exception.Message
    };

    /// <summary>開きかけたものを閉じる。閉じる途中の失敗は、開けなかった理由を覆い隠さないよう捨てる。</summary>
    private static void Close(SshClient? client, ForwardedPortLocal? forwarded)
    {
        try
        {
            forwarded?.Dispose();
        }
        catch (SshException)
        {
        }

        client?.Dispose();
    }
}
