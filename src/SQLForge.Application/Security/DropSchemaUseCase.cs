using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Security;

/// <summary>
/// スキーマを 1 件削除する。取り消せない操作なので、
/// 触ってよい相手かどうかはサーバーへ送る前にここでも見る。
/// </summary>
public sealed class DropSchemaUseCase
{
    public Task ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaDescriptor schema,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.IsSystem)
        {
            throw new SchemaRejectedException("システムのスキーマは削除できません。");
        }

        return session.DropSchemaAsync(database, schema.Name, cancellationToken);
    }
}
