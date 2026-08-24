using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Security;

/// <summary>
/// ユーザーを 1 件保存する。新規なら作成、編集なら変更として渡す。
/// どちらになるかは下書きが元の姿を持っているかだけで決まる。
/// </summary>
public sealed class SaveDatabaseUserUseCase
{
    public async Task<DatabaseUserValidationResult> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseUserDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);

        var validation = DatabaseUserValidator.Validate(draft);

        if (!validation.IsValid)
        {
            return validation;
        }

        var definition = draft.ToDefinition();

        if (draft.Original is { } original)
        {
            await session.AlterDatabaseUserAsync(database, original, definition, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await session.CreateDatabaseUserAsync(database, definition, cancellationToken).ConfigureAwait(false);
        }

        return validation;
    }
}
