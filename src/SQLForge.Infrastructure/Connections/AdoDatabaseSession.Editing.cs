using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 編集グリッド（先頭 N 行を編集）の受け持ち。
///
/// 読むところと流すところは ADO.NET の作法だけで書けるのでここに置き、
/// 文面の組み立て（TOP と LIMIT の違い、識別子の引用符、型ごとの値の作り方）だけを
/// ドライバーへ預ける。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task<EditableRowSet> ReadEditableRowsAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        int maxRows,
        CancellationToken cancellationToken = default) =>
        QueryAsync(
            async (connection, token) =>
            {
                var columns = await EditableColumnsAsync(connection, database, schema, table, token)
                    .ConfigureAwait(false);

                // 文面は修飾なしの 2 部名で組むので、先に実行先へ合わせる。
                await SwitchDatabaseAsync(connection, database, token).ConfigureAwait(false);

                return await ReadEditableRowsAsync(
                        connection,
                        BuildTopRowsSelect(schema, table, columns, maxRows),
                        columns,
                        maxRows,
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<int> UpdateTableCellAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        TableCellUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return QueryAsync(
            async (connection, token) =>
            {
                // 型は毎回読み直す。文字列で受け取った値を列の型へ写すのに要るうえ、
                // 読み込んだあとに定義が変わっていれば、ここで気付ける。
                var columns = await EditableColumnsAsync(connection, database, schema, table, token)
                    .ConfigureAwait(false);

                await SwitchDatabaseAsync(connection, database, token).ConfigureAwait(false);

                return await ExecuteUpdateAsync(
                        connection,
                        BuildCellUpdate(schema, table, columns, update),
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>編集グリッドに出す列の素性。鍵にできるか・書き換えられるかまで実装側が決める。</summary>
    protected abstract Task<IReadOnlyList<EditableColumn>> ReadEditableColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken);

    /// <summary>先頭 <paramref name="maxRows"/> 行を読む文面。行数の絞り方はエンジンごとに違う。</summary>
    protected abstract ParameterizedStatement BuildTopRowsSelect(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        int maxRows);

    /// <summary>セル 1 つを書き戻す文面。表示用の文字列を列の型へ写すのもここで行う。</summary>
    protected abstract ParameterizedStatement BuildCellUpdate(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableCellUpdate update);

    private async Task<IReadOnlyList<EditableColumn>> EditableColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken)
    {
        var columns = await ReadEditableColumnsAsync(connection, database, schema, table, cancellationToken)
            .ConfigureAwait(false);

        return columns.Count > 0
            ? columns
            : throw new InvalidOperationException($"{schema.Value}.{table} の列定義を読み取れませんでした。");
    }

    /// <summary>
    /// 行を読む。列は文面で指定した並びそのものなので、リーダーの位置で対応が取れる。
    /// </summary>
    private static async Task<EditableRowSet> ReadEditableRowsAsync(
        DbConnection connection,
        ParameterizedStatement statement,
        IReadOnlyList<EditableColumn> columns,
        int maxRows,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, statement);

        var rows = new List<IReadOnlyList<string?>>();
        var isTruncated = false;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // 上限ちょうどを求めた文面でも、1 行多く返るエンジンに備えて数え直す。
            if (rows.Count >= maxRows)
            {
                isTruncated = true;
                break;
            }

            var values = new string?[columns.Count];

            for (var ordinal = 0; ordinal < values.Length; ordinal++)
            {
                values[ordinal] = reader.IsDBNull(ordinal) ? null : AdoValueText.From(reader.GetValue(ordinal));
            }

            rows.Add(values);
        }

        return new EditableRowSet(columns, rows, isTruncated);
    }

    /// <summary>
    /// 更新を 1 つ流す。条件に 2 行以上が当たったら巻き戻す。
    ///
    /// 編集グリッドの操作は「画面のこの行を直す」なので、思っていたより多くの行に
    /// 当たった時点でそれは別のことをしている。コミットへ辿り着かなければ破棄が巻き戻す。
    /// </summary>
    private static async Task<int> ExecuteUpdateAsync(
        DbConnection connection,
        ParameterizedStatement statement,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, statement);
        command.Transaction = transaction;

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (affected > 1)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

            throw new InvalidOperationException(
                $"条件に {affected} 行が当たったため、更新を取り消しました。行を 1 件に特定できません。");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return affected;
    }

    private static DbCommand Command(DbConnection connection, ParameterizedStatement statement)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement.Text;

        for (var ordinal = 0; ordinal < statement.Parameters.Count; ordinal++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = ParameterizedStatement.NameOf(ordinal);
            parameter.Value = statement.Parameters[ordinal] ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
