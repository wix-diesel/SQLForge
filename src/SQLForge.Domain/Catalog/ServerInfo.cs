namespace SQLForge.Domain.Catalog;

/// <summary>
/// 接続先サーバーの素性。接続テストの結果表示とステータスバーに出す。
/// <see cref="IsEncrypted"/> だけはサーバーではなくこのセッションの事実で、
/// 経路が暗号化されているかをサーバーに尋ねられなかったときは null になる。
/// </summary>
public sealed record ServerInfo(string ProductName, string Version, string Edition = "", bool? IsEncrypted = null)
{
    /// <summary>例: SQL Server 2022 (16.0.4215.2)</summary>
    public string Description => string.IsNullOrEmpty(Version) ? ProductName : $"{ProductName} ({Version})";
}
