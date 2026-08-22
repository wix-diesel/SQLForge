using System.Collections.Concurrent;
using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Security;

/// <summary>
/// キーリングのプロセス内代替。実装予定は Linux = Secret Service (org.freedesktop.secrets)、
/// Windows = 資格情報マネージャー、macOS = キーチェーン。
/// この版ではプロセス内に持つだけで、ディスクには一切書かない。
/// </summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public bool IsAvailable => true;

    public string DisplayName => "キーリング (未接続・プロセス内保持)";

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        _secrets[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_secrets.TryGetValue(key, out var secret) ? secret : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _secrets.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
