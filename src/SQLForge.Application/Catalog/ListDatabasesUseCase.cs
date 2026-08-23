using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>
/// サーバー上のデータベース一覧。オブジェクトエクスプローラーの「データベース」を開いたときに走る。
/// システムデータベースも隠さずに出す（master へ繋いだのに一覧に無い、を避けるため）。並びだけ後ろへ回す。
/// </summary>
public sealed class ListDatabasesUseCase
{
    public async Task<IReadOnlyList<DatabaseDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var databases = await session.ListDatabasesAsync(cancellationToken).ConfigureAwait(false);

        return CatalogOrdering
            .UserObjectsFirst(databases, database => database.IsSystem, database => database.Name.Value)
            .ToList();
    }
}
