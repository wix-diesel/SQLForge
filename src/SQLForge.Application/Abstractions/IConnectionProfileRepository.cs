using SQLForge.Domain.Connections;

namespace SQLForge.Application.Abstractions;

/// <summary>保存済み接続の永続化ポート。実装は Infrastructure 側に置く。</summary>
public interface IConnectionProfileRepository
{
    Task<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task<ConnectionProfile?> FindAsync(ConnectionProfileId id, CancellationToken cancellationToken = default);

    /// <summary>同じ Id があれば上書き、なければ追加する。</summary>
    Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(ConnectionProfileId id, CancellationToken cancellationToken = default);
}
