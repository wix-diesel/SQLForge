using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>ストアド プロシージャのパラメーター一覧。宣言順に揃える。</summary>
public sealed class ListStoredProcedureParametersUseCase
{
    public async Task<IReadOnlyList<StoredProcedureParameterDescriptor>> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaName schema,
        string procedure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var parameters = await session
            .ListStoredProcedureParametersAsync(database, schema, procedure, cancellationToken)
            .ConfigureAwait(false);

        return parameters.OrderBy(parameter => parameter.OrdinalPosition).ToList();
    }
}
