using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 接続ダイアログで編集中の入力値。エンティティ（<see cref="ConnectionProfile"/>）は
/// 常に妥当である前提なので、まだ妥当とは限らない入力はこの器で受け渡す。
/// </summary>
public sealed record ConnectionDraft
{
    public required ConnectionProfileId Id { get; init; }

    public required string Name { get; init; }

    public required EnvironmentTag Environment { get; init; }

    public required DatabaseDriver Driver { get; init; }

    public required string Host { get; init; }

    /// <summary>未入力・数値でない場合は 0。検証で弾く。</summary>
    public required int Port { get; init; }

    public required string Database { get; init; }

    public required string UserName { get; init; }

    public required AuthenticationMethod Authentication { get; init; }

    public required bool StoreSecretInKeyring { get; init; }

    public required TlsMode Tls { get; init; }

    public required AccessMode AccessMode { get; init; }

    public static ConnectionDraft FromProfile(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ConnectionDraft
        {
            Id = profile.Id,
            Name = profile.Name,
            Environment = profile.Environment,
            Driver = profile.Target.Driver,
            Host = profile.Target.Address.Host,
            Port = profile.Target.Address.Port,
            Database = profile.Target.Database,
            UserName = profile.Credentials.UserName,
            Authentication = profile.Credentials.Method,
            StoreSecretInKeyring = profile.Credentials.StoreSecretInKeyring,
            Tls = profile.Target.Tls,
            AccessMode = profile.AccessMode
        };
    }

    /// <summary>検証を通ったあとにだけ呼ぶこと。</summary>
    public ConnectionProfile ToProfile() =>
        new(Id,
            Name,
            Environment,
            new ConnectionTarget(Driver, new ServerAddress(Host, Port), Database, Tls),
            new ConnectionCredentials(UserName, Authentication, StoreSecretInKeyring),
            AccessMode);

    /// <summary>検証前でも出せる接続 URL（下部の確認用表示）。</summary>
    public string ToUrl()
    {
        var host = string.IsNullOrWhiteSpace(Host) ? "…" : Host.Trim();
        var target = new ConnectionTarget(Driver, new ServerAddress(host, Port), Database, Tls);
        return ConnectionUrl.Build(target, new ConnectionCredentials(UserName, Authentication, StoreSecretInKeyring));
    }
}
