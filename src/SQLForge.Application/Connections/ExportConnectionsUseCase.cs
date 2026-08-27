using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 保存済み接続を 1 枚のファイルへ書き出す（SSMS の「登録済みサーバーのエクスポート」）。
///
/// SSMS と同じく、ユーザー名とパスワードを含めるかどうかを選べる。含めないときは
/// 利用者名を空にして書き出し、取り込んだ先で入れ直してもらう。
/// </summary>
public sealed class ExportConnectionsUseCase(
    IConnectionProfileRepository repository,
    ISecretStore secretStore,
    IConnectionArchive archive)
{
    private readonly IConnectionProfileRepository _repository = repository;
    private readonly ISecretStore _secretStore = secretStore;
    private readonly IConnectionArchive _archive = archive;

    /// <summary>書き出す。<paramref name="ids"/> が空なら保存済みのすべてが対象。</summary>
    /// <returns>書き出した件数。</returns>
    public async Task<int> ExecuteAsync(
        string path,
        IReadOnlyCollection<ConnectionProfileId> ids,
        bool includeCredentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var profiles = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        var selected = ids.Count == 0
            ? profiles.ToList()
            : profiles.Where(profile => ids.Contains(profile.Id)).ToList();

        var connections = new List<ArchivedConnection>(selected.Count);
        foreach (var profile in selected)
        {
            connections.Add(await ToArchivedAsync(profile, includeCredentials, cancellationToken).ConfigureAwait(false));
        }

        await _archive.WriteAsync(path, connections, cancellationToken).ConfigureAwait(false);

        return connections.Count;
    }

    private async Task<ArchivedConnection> ToArchivedAsync(
        ConnectionProfile profile,
        bool includeCredentials,
        CancellationToken cancellationToken) =>
        includeCredentials
            ? new ArchivedConnection(profile, await ReadSecretAsync(profile, cancellationToken).ConfigureAwait(false))
            : new ArchivedConnection(WithoutUserName(profile), null);

    /// <summary>「ユーザー名とパスワードを書き出さない」を選んだときの姿。</summary>
    private static ConnectionProfile WithoutUserName(ConnectionProfile profile) =>
        new(profile.Id,
            profile.Name,
            profile.Environment,
            profile.Target,
            new ConnectionCredentials(
                string.Empty,
                profile.Credentials.Method,
                profile.Credentials.StoreSecretInKeyring),
            profile.AccessMode);

    /// <summary>預けてある資格情報を取り出す。預けていない接続では何も持たせない。</summary>
    private async Task<string?> ReadSecretAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        if (!_secretStore.IsAvailable
            || !profile.Credentials.RequiresSecret
            || !profile.Credentials.StoreSecretInKeyring)
        {
            return null;
        }

        return await _secretStore.ReadAsync(SaveConnectionUseCase.SecretKeyFor(profile), cancellationToken)
            .ConfigureAwait(false);
    }
}
