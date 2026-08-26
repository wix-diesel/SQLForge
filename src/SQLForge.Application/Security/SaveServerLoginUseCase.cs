using SQLForge.Application.Abstractions;

namespace SQLForge.Application.Security;

/// <summary>
/// ログインを 1 件保存する。新規なら作成、編集なら変更として渡す。
/// どちらになるかは下書きが元の姿を持っているかだけで決まる。
/// </summary>
public sealed class SaveServerLoginUseCase
{
    public async Task<SecurityValidationResult> ExecuteAsync(
        IDatabaseSession session,
        ServerLoginDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ServerLoginValidator.Validate(draft);

        if (!validation.IsValid)
        {
            return validation;
        }

        var definition = draft.ToDefinition();

        if (draft.Original is { } original)
        {
            await session.AlterServerLoginAsync(original, definition, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await session.CreateServerLoginAsync(definition, cancellationToken).ConfigureAwait(false);
        }

        return validation;
    }
}
