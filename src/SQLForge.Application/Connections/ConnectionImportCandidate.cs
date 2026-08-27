using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 取り込もうとしている接続 1 件と、それが当たった手元の接続。
///
/// <see cref="Existing"/> が入っているものは、置き換えるか飛ばすかを利用者に尋ねてから
/// <see cref="ImportConnectionsUseCase.ApplyAsync"/> へ渡す（SSMS の取り込みと同じ）。
/// </summary>
public sealed record ConnectionImportCandidate(
    ConnectionProfile Profile,
    string? Secret,
    ConnectionProfile? Existing)
{
    /// <summary>手元の接続に当たったか。当たったものだけを尋ねる。</summary>
    public bool ConflictsWithExisting => Existing is not null;
}
