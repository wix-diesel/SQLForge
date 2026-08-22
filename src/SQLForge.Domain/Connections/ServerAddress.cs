namespace SQLForge.Domain.Connections;

/// <summary>ホストとポートの組。ファイルベースのドライバーではパスを host に入れる。</summary>
public sealed record ServerAddress
{
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    public ServerAddress(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("ホストは空にできません。", nameof(host));
        }

        Host = host.Trim();
        Port = port;
    }

    public string Host { get; }

    /// <summary>0 はポートを持たない接続（SQLite など）を表す。</summary>
    public int Port { get; }

    public bool HasPort => Port >= MinPort && Port <= MaxPort;

    public static bool IsValidPort(int port) => port is >= MinPort and <= MaxPort;

    public override string ToString() => HasPort ? $"{Host}:{Port}" : Host;
}
