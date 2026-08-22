using System.Collections.Concurrent;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 保存済み接続のインメモリ実装。永続化（~/.config/sqlforge/connections.toml）が
/// 入るまでの差し替え用で、プロセスを終えると内容は消える。
/// </summary>
public sealed class InMemoryConnectionProfileRepository : IConnectionProfileRepository
{
    private readonly ConcurrentDictionary<ConnectionProfileId, ConnectionProfile> _profiles = new();
    private readonly List<ConnectionProfileId> _order = [];
    private readonly object _gate = new();

    // DI コンテナが IEnumerable<T> を空コレクションとして解決してしまうため、
    // 一覧を受け取る口はコンストラクタではなく静的メソッドにしてある。
    public InMemoryConnectionProfileRepository()
        : this(SeedConnections.Create(), seeded: true)
    {
    }

    private InMemoryConnectionProfileRepository(IEnumerable<ConnectionProfile> profiles, bool seeded)
    {
        _ = seeded;

        foreach (var profile in profiles)
        {
            Put(profile);
        }
    }

    /// <summary>任意の内容で作る（テスト用）。</summary>
    public static InMemoryConnectionProfileRepository With(IEnumerable<ConnectionProfile> profiles) =>
        new(profiles, seeded: false);

    public Task<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ConnectionProfile> snapshot = _order
                .Select(id => _profiles[id])
                .ToList();

            return Task.FromResult(snapshot);
        }
    }

    public Task<ConnectionProfile?> FindAsync(ConnectionProfileId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_profiles.TryGetValue(id, out var profile) ? profile : null);

    public Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Put(profile);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ConnectionProfileId id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _profiles.TryRemove(id, out _);
            _order.Remove(id);
        }

        return Task.CompletedTask;
    }

    private void Put(ConnectionProfile profile)
    {
        lock (_gate)
        {
            if (_profiles.TryAdd(profile.Id, profile))
            {
                _order.Add(profile.Id);
                return;
            }

            _profiles[profile.Id] = profile;
        }
    }
}
