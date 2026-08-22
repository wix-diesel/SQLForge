using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>保存済み接続を検索して環境タグごとにまとめる。接続ダイアログ左ペインの内容。</summary>
public sealed class ListSavedConnectionsUseCase(IConnectionProfileRepository repository)
{
    private readonly IConnectionProfileRepository _repository = repository;

    public async Task<IReadOnlyList<SavedConnectionGroup>> ExecuteAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var profiles = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        var matched = profiles.Where(profile => profile.Matches(searchTerm ?? string.Empty));

        return Group(matched);
    }

    private static IReadOnlyList<SavedConnectionGroup> Group(IEnumerable<ConnectionProfile> profiles) =>
        profiles
            .GroupBy(profile => profile.Environment)
            .OrderBy(group => EnvironmentTag.All.ToList().IndexOf(group.Key))
            .Select(group => new SavedConnectionGroup(
                group.Key,
                group.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
}
