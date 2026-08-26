using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Security;

/// <summary>
/// データベース ロールを 1 件保存する。新規なら作成、編集なら変更として渡す。
/// どちらになるかは下書きが元の姿を持っているかだけで決まる。
/// </summary>
public sealed class SaveDatabaseRoleUseCase
{
    public async Task<SecurityValidationResult> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseRoleDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);

        var validation = DatabaseRoleValidator.Validate(draft);

        if (!validation.IsValid)
        {
            return validation;
        }

        var definition = draft.ToDefinition();

        if (draft.Original is { } original)
        {
            await session.AlterDatabaseRoleAsync(database, original, definition, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await session.CreateDatabaseRoleAsync(database, definition, cancellationToken).ConfigureAwait(false);
        }

        return validation;
    }
}
