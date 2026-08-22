namespace SQLForge.Domain.Connections;

/// <summary>保存済み接続の識別子。</summary>
public readonly record struct ConnectionProfileId(Guid Value)
{
    public static ConnectionProfileId New() => new(Guid.NewGuid());

    public static ConnectionProfileId Parse(string value) => new(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
