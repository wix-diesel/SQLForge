using SQLForge.Application.Abstractions;

namespace SQLForge.Infrastructure.Security;

/// <summary>
/// キーリングを使えない環境の受け皿。どの OS の預け先も名乗り出なかったときや、
/// 名乗り出たものが「今は使えない」と言ったときに使う。
///
/// <see cref="UnknownPlatformProfile"/> と同じ考え方で、起動できなくなるより
/// 「キーリングを利用できません」と伝えて、パスワードの都度入力で動かす。
/// </summary>
public sealed class UnavailableSecretStore : ISecretStore
{
    /// <summary>キーリングが無いときの言い方。OS ごとの預け先も使えないときはこれを名乗る。</summary>
    public const string UnavailableName = "キーリングを利用できません";

    public bool IsAvailable => false;

    public string DisplayName => UnavailableName;

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
