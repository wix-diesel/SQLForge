using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Security;

/// <summary>
/// 権限の変更を 1 度に流す。何を GRANT して何を REVOKE するのかの割り出しは、
/// 文面の作法がエンジンごとに違うのでドライバーへ任せ、ここは前後の姿を渡すだけにする。
/// </summary>
public sealed class SavePermissionsUseCase
{
    public async Task<SecurityValidationResult> ExecuteAsync(
        IDatabaseSession session,
        PermissionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);

        var validation = PermissionValidator.Validate(draft);

        if (!validation.IsValid)
        {
            return validation;
        }

        await session.ApplyPermissionsAsync(
                draft.Principal,
                draft.Database,
                draft.Original,
                draft.Entries,
                cancellationToken)
            .ConfigureAwait(false);

        return validation;
    }
}
