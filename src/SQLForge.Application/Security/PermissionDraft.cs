using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// 「セキュリティ保護可能なリソース」ページで編集中の権限一式。
///
/// <see cref="Entries"/> はグリッドに出ている行をすべて含む。外したチェックは
/// <see cref="PermissionState.Revoked"/> の行として残り、行ごと消えるのではない。
/// 出ていない権限（この版が知らない新しい権限など）は <see cref="Original"/> にしか
/// 現れず、そのまま手を触れずに残る。
/// </summary>
public sealed record PermissionDraft
{
    /// <summary>権限の持ち主。</summary>
    public required SecurityPrincipal Principal { get; init; }

    /// <summary>
    /// データベース スコープの主体（ユーザー・データベース ロール）がいるデータベース。
    /// サーバー スコープの主体では null。
    /// </summary>
    public DatabaseName? Database { get; init; }

    /// <summary>開いたときにサーバーから読んだ姿。</summary>
    public IReadOnlyList<PermissionEntry> Original { get; init; } = [];

    /// <summary>グリッドの今の姿。</summary>
    public IReadOnlyList<PermissionEntry> Entries { get; init; } = [];
}
