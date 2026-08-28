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
/// <param name="CreatedAt">作成された日時。ツリーの絞り込み（作成日）に使う。読めないときは null。</param>
public sealed record DatabaseDescriptor(
    DatabaseName Name,
    bool IsSystem = false,
    bool IsAccessible = true,
    string? Collation = null,
    DateTime? CreatedAt = null);
