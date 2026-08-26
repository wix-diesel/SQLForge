using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// テーブルの先頭 N 行を編集用に読む。
///
/// SSMS の「上位 200 行の編集」にあたる入口で、こちらは 100 行にしてある。
/// 並び順は指定しない（SSMS も指定しない。ORDER BY を付けると、
/// キーの無いテーブルや大きなテーブルで読み込みが重くなるため）。
/// </summary>
public sealed class EditTableRowsUseCase
{
    /// <summary>編集グリッドに読む既定の行数。</summary>
    public const int DefaultMaxRows = 100;

    public async Task<EditableRowSet> ExecuteAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaName schema,
        string table,
        int maxRows = DefaultMaxRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(table))
        {
            throw new ArgumentException("テーブル名は空にできません。", nameof(table));
        }

        return await session
            .ReadEditableRowsAsync(database, schema, table, maxRows < 1 ? 1 : maxRows, cancellationToken)
            .ConfigureAwait(false);
    }
}
