using System.Data.Common;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;
using SQLForge.Infrastructure.Connections;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 編集グリッド（先頭 N 行を編集）の SQL Server 側。
///
/// 「どの列を鍵にするか」「どの列を書き換えられるか」はエンジンの型と列の性質で決まるので、
/// その判断はここで済ませ、上の層へは結果だけを渡す。
/// </summary>
public sealed partial class SqlServerSession
{
    protected override async Task<IReadOnlyList<EditableColumn>> ReadEditableColumnsAsync(
        DbConnection connection,
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Format(SqlServerCatalogQueries.EditableColumnsFormat, database);

        AddParameter(command, "@schema", schema.Value);
        AddParameter(command, "@table", table);

        var columns = new List<RawColumn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new RawColumn(
                Name: reader.GetString(0),
                BaseType: reader.GetString(1),
                DataType: SqlServerTypeFormat.Describe(
                    reader.GetString(1), reader.GetInt16(2), reader.GetByte(3), reader.GetByte(4)),
                IsNullable: reader.GetBoolean(5),
                IsIdentity: reader.GetBoolean(6),
                IsComputed: reader.GetBoolean(7),
                IsPrimaryKey: reader.GetBoolean(8)));
        }

        return Describe(columns);
    }

    protected override ParameterizedStatement BuildTopRowsSelect(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        int maxRows) =>
        SqlServerEditStatements.TopRows(schema, table, columns, maxRows);

    protected override ParameterizedStatement BuildCellUpdate(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableCellUpdate update) =>
        SqlServerEditStatements.CellUpdate(schema, table, columns, update);

    protected override ParameterizedStatement BuildRowInsert(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowInsert insert) =>
        SqlServerEditStatements.RowInsert(schema, table, columns, insert);

    protected override ParameterizedStatement BuildRowDelete(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowDelete delete) =>
        SqlServerEditStatements.RowDelete(schema, table, columns, delete);

    /// <summary>
    /// 行を特定する鍵を決めてから、列の素性を組む。
    ///
    /// 主キーがあればその列だけを鍵にする。無いテーブルでは、比較できる列すべてを
    /// 条件に並べて 1 行に絞り込む（SSMS の編集グリッドと同じ）。それでも 2 行以上に
    /// 当たる行は書き換えられないが、それは実際に投げたときに取り消される。
    /// </summary>
    private static IReadOnlyList<EditableColumn> Describe(IReadOnlyList<RawColumn> columns)
    {
        var hasPrimaryKey = columns.Any(column => column.IsPrimaryKey);

        return columns
            .Select(column => new EditableColumn(
                column.Name,
                column.DataType,
                column.IsNullable,
                IsKey: hasPrimaryKey
                    ? column.IsPrimaryKey
                    : SqlServerCellValue.IsComparable(column.BaseType),
                // IDENTITY と計算列はサーバーが値を決める。扱えない型（バイナリなど）も読むだけにする。
                IsReadOnly: column.IsIdentity
                    || column.IsComputed
                    || !SqlServerCellValue.IsEditable(column.BaseType),
                IsNumeric: SqlServerCellValue.IsNumeric(column.BaseType),
                IsText: SqlServerCellValue.IsText(column.BaseType),
                // 足した行を読み直すときに SCOPE_IDENTITY() で当てられる列。
                IsIdentity: column.IsIdentity))
            .ToList();
    }

    /// <summary>パラメータを 1 つ足す。値は文字列とは限らない（SID のようなバイト列もある）。</summary>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>カタログから読んだままの 1 列。鍵の決め方は全列を見てからでないと決まらない。</summary>
    private sealed record RawColumn(
        string Name,
        string BaseType,
        string DataType,
        bool IsNullable,
        bool IsIdentity,
        bool IsComputed,
        bool IsPrimaryKey);
}
