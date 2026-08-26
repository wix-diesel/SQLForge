using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Security;

/// <summary>
/// スキーマを 1 件保存する。新規なら作成、編集なら所有者の付け替えとして渡す。
/// どちらになるかは下書きが元の姿を持っているかだけで決まる。
/// </summary>
public sealed class SaveSchemaUseCase
{
    public async Task<SecurityValidationResult> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);

        var validation = SchemaValidator.Validate(draft);

        if (!validation.IsValid)
        {
            return validation;
        }

        var definition = draft.ToDefinition();

        if (draft.Original is { } original)
        {
            await session.AlterSchemaAsync(database, original, definition, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await session.CreateSchemaAsync(database, definition, cancellationToken).ConfigureAwait(false);
        }

        return validation;
    }
}
