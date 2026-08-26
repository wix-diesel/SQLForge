using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 編集グリッドの行を足す・消す受け持ち。
///
/// 読み書きの作法は <see cref="AdoDatabaseSession"/> のもう一枚（Editing）と同じで、
/// 文面の組み立てだけをドライバーへ預ける。足した行をその場で読み直すのは、
/// IDENTITY や既定値でサーバーが決めた値を画面へ写すため。
/// </summary>
public abstract partial class AdoDatabaseSession
{
    public Task<IReadOnlyList<string?>?> InsertTableRowAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        TableRowInsert insert,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(insert);

        return QueryAsync<IReadOnlyList<string?>?>(
            async (connection, token) =>
            {
                var columns = await EditableColumnsAsync(connection, database, schema, table, token)
                    .ConfigureAwait(false);

                await SwitchDatabaseAsync(connection, database, token).ConfigureAwait(false);

                return await ExecuteInsertAsync(
                        connection,
                        BuildRowInsert(schema, table, columns, insert),
                        columns.Count,
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<int> DeleteTableRowAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        TableRowDelete delete,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delete);

        return QueryAsync(
            async (connection, token) =>
            {
                var columns = await EditableColumnsAsync(connection, database, schema, table, token)
                    .ConfigureAwait(false);

                await SwitchDatabaseAsync(connection, database, token).ConfigureAwait(false);

                return await ExecuteWriteAsync(
                        connection,
                        BuildRowDelete(schema, table, columns, delete),
                        "削除",
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// 行 1 つを足す文面。足した行を読み直す文面（採番された値を拾うため）まで含めてよい。
    /// 読み直しを付けるかどうか・どう組むかはエンジンごとに違う。
    /// </summary>
    protected abstract ParameterizedStatement BuildRowInsert(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowInsert insert);

    /// <summary>行 1 つを消す文面。</summary>
    protected abstract ParameterizedStatement BuildRowDelete(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowDelete delete);

    /// <summary>
    /// 行を 1 つ足して、足した行を読み直す。文面に読み直しが付いていなければ null を返す。
    ///
    /// 足すのと読み直すのを 1 つのトランザクションに入れるのは、読み直しで落ちたときに
    /// 「画面には出ないが入っている行」を残さないため。
    /// </summary>
    private static async Task<IReadOnlyList<string?>?> ExecuteInsertAsync(
        DbConnection connection,
        ParameterizedStatement statement,
        int columnCount,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, statement);
        command.Transaction = transaction;

        var inserted = await ReadInsertedRowAsync(command, columnCount, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return inserted;
    }

    /// <summary>
    /// 足した行を読む。INSERT そのものは結果を返さないので、後ろに続く読み直しの
    /// 1 行だけを拾う（読み直しが付いていない文面では何も返らない）。
    /// </summary>
    private static async Task<IReadOnlyList<string?>?> ReadInsertedRowAsync(
        DbCommand command,
        int columnCount,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }

        // 列の数が合わないのは、読み込んだあとに定義が変わったとき。読み直してもらう。
        if (reader.FieldCount != columnCount)
        {
            return null;
        }

        var values = new string?[columnCount];

        for (var ordinal = 0; ordinal < values.Length; ordinal++)
        {
            values[ordinal] = reader.IsDBNull(ordinal) ? null : AdoValueText.From(reader.GetValue(ordinal));
        }

        return values;
    }
}
