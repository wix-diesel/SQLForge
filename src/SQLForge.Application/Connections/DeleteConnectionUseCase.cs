using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 保存済み接続を消す。台帳から消すだけでなく、預けてある資格情報も一緒に始末する
/// （残しておくと、同じ Id が振り直されない限り誰も読まない値がキーリングに溜まる）。
///
/// 確認を取るのは呼び出し側（左ペイン）の受け持ちで、ここへ来た時点では消してよい。
/// </summary>
public sealed class DeleteConnectionUseCase(IConnectionProfileRepository repository, ISecretStore secretStore)
{
    private readonly IConnectionProfileRepository _repository = repository;
    private readonly ISecretStore _secretStore = secretStore;

    public async Task ExecuteAsync(ConnectionProfileId id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        if (_secretStore.IsAvailable)
        {
            await _secretStore.DeleteAsync(SaveConnectionUseCase.SecretKeyFor(id), cancellationToken)
                .ConfigureAwait(false);

            // 踏み台のぶんも同じ理由で始末する。
            await _secretStore.DeleteAsync(SaveConnectionUseCase.SshSecretKeyFor(id), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
