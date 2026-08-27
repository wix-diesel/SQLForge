using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 書き出したファイルから接続を取り込む（SSMS の「登録済みサーバーのインポート」）。
///
/// 読むところと書くところを分けてある。手元の接続に当たったものをどうするかは
/// 利用者に尋ねる必要があり、尋ねるのはビュー側の受け持ちだから
/// （<see cref="ReadAsync"/> で当たりを調べ、尋ね終えたものだけ <see cref="ApplyAsync"/> へ渡す）。
/// </summary>
public sealed class ImportConnectionsUseCase(
    IConnectionProfileRepository repository,
    ISecretStore secretStore,
    IConnectionArchive archive)
{
    private readonly IConnectionProfileRepository _repository = repository;
    private readonly ISecretStore _secretStore = secretStore;
    private readonly IConnectionArchive _archive = archive;

    /// <summary>ファイルを読み、手元の接続に当たるかどうかまで調べる。まだ何も書かない。</summary>
    public async Task<IReadOnlyList<ConnectionImportCandidate>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var incoming = await _archive.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        var existing = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);

        return incoming
            .Select(connection => new ConnectionImportCandidate(
                connection.Profile,
                connection.Secret,
                FindExisting(existing, connection.Profile)))
            .ToList();
    }

    /// <summary>尋ね終えたものを保存済み接続へ反映する。</summary>
    /// <returns>取り込んだ件数。</returns>
    public async Task<int> ApplyAsync(
        IEnumerable<ConnectionImportCandidate> accepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        var count = 0;
        foreach (var candidate in accepted)
        {
            await RemoveReplacedAsync(candidate, cancellationToken).ConfigureAwait(false);
            await _repository.SaveAsync(candidate.Profile, cancellationToken).ConfigureAwait(false);
            await PersistSecretAsync(candidate, cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }

    /// <summary>
    /// 「同じ接続」の見方。書き出したファイルを同じ環境へ戻すときは Id で当たり、
    /// 別の環境から持ってきたときは Id が違うので、SSMS と同じく名前で見る
    /// （名前が同じでも環境タグが違えば別の接続として足す）。
    /// </summary>
    private static ConnectionProfile? FindExisting(
        IReadOnlyList<ConnectionProfile> existing,
        ConnectionProfile incoming) =>
        existing.FirstOrDefault(profile => profile.Id == incoming.Id)
        ?? existing.FirstOrDefault(profile =>
            profile.Environment.Equals(incoming.Environment)
            && string.Equals(profile.Name, incoming.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 名前で当たったもの（Id が違うもの）を置き換えるときは、先に古いほうを消す。
    /// Id で当たったものは <see cref="IConnectionProfileRepository.SaveAsync"/> の上書きで済む。
    /// </summary>
    private async Task RemoveReplacedAsync(ConnectionImportCandidate candidate, CancellationToken cancellationToken)
    {
        if (candidate.Existing is not { } existing || existing.Id == candidate.Profile.Id)
        {
            return;
        }

        await _repository.DeleteAsync(existing.Id, cancellationToken).ConfigureAwait(false);

        if (_secretStore.IsAvailable)
        {
            await _secretStore.DeleteAsync(SaveConnectionUseCase.SecretKeyFor(existing), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>ファイルにパスワードが入っていたときだけ、キーリングへ預け直す。</summary>
    private async Task PersistSecretAsync(ConnectionImportCandidate candidate, CancellationToken cancellationToken)
    {
        if (candidate.Secret is not { Length: > 0 } secret || !_secretStore.IsAvailable)
        {
            return;
        }

        await _secretStore
            .SaveAsync(SaveConnectionUseCase.SecretKeyFor(candidate.Profile), secret, cancellationToken)
            .ConfigureAwait(false);
    }
}
