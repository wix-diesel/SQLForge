namespace SQLForge.Domain.Catalog;

/// <summary>
/// サーバー上のデータベース 1 件。エンジン共通で扱える事柄だけを持つ。
/// </summary>
/// <param name="Name">データベース名。</param>
/// <param name="IsSystem">エンジンが用意したデータベース（SQL Server の master / msdb など）。</param>
/// <param name="IsAccessible">
/// この接続で中身を開けるか。オフラインのデータベースや権限のないデータベースは false。
/// false のものも一覧には出すが、展開はさせない。
/// </param>
/// <param name="Collation">照合順序。取得できないエンジンでは null。</param>
public sealed record DatabaseDescriptor(
    DatabaseName Name,
    bool IsSystem = false,
    bool IsAccessible = true,
    string? Collation = null);
