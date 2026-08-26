using SQLForge.Application.Abstractions;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ログイン 1 件のユーザー マッピング。SSMS の「ユーザー マッピング」ページに並べるもので、
/// データベース名順に揃える。
/// </summary>
public sealed class ListLoginUserMappingsUseCase
{
    public async Task<IReadOnlyList<LoginUserMapping>> ExecuteAsync(
        IDatabaseSession session,
        ServerLoginName login,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var mappings = await session.ListLoginUserMappingsAsync(login, cancellationToken).ConfigureAwait(false);

        return mappings
            .OrderBy(mapping => mapping.Database.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
