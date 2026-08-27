using System.Globalization;
using System.Text;
using SQLForge.Application.Editing;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;
using SQLForge.Infrastructure.Connections;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 編集グリッドが投げる文面。値はすべてパラメータで渡し、識別子だけを角括弧で囲む。
///
/// 実行先のデータベースへは切り替え済みなので、名前は 2 部名（スキーマ.テーブル）で書く。
/// </summary>
internal static class SqlServerEditStatements
{
    /// <summary>
    /// 先頭 <paramref name="maxRows"/> 行を読む文面。
    ///
    /// 並び順は指定しない（SSMS の「上位 200 行の編集」も指定しない）。
    /// 列は <c>*</c> ではなく名前を並べる。グリッドの列の並びと読み取りの位置を必ず一致させるため。
    /// </summary>
    public static ParameterizedStatement TopRows(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        int maxRows)
    {
        var names = string.Join(", ", columns.Select(column => Quote(column.Name)));

        return new ParameterizedStatement(
            $"SELECT TOP ({ParameterizedStatement.NameOf(0)}) {names} FROM {Quote(schema.Value)}.{Quote(table)};",
            [maxRows]);
    }

    /// <summary>
    /// セル 1 つを書き戻す文面。条件は変更前の値で組み、NULL は <c>IS NULL</c> で比べる
    /// （<c>= NULL</c> は常に不定になり、どの行にも当たらない）。
    /// </summary>
    public static ParameterizedStatement CellUpdate(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableCellUpdate update)
    {
        var target = Find(columns, update.Column);

        if (target.IsReadOnly)
        {
            throw new TableEditRejectedException($"{target.Name} は編集できない列です。");
        }

        var values = new List<object?> { ToParameter(target, update.Value) };
        var conditions = Conditions(columns, update.Criteria, values);

        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"UPDATE {Quote(schema.Value)}.{Quote(table)}")
            .Append(CultureInfo.InvariantCulture, $" SET {Quote(target.Name)} = {ParameterizedStatement.NameOf(0)}")
            .Append(CultureInfo.InvariantCulture, $" WHERE {string.Join(" AND ", conditions)};");

        return new ParameterizedStatement(text.ToString(), values);
    }

    /// <summary>
    /// 行 1 つを足す文面。打ち込まれた列だけを並べ、触っていない列はサーバーの既定値に任せる。
    ///
    /// 足したあとに、その行をもう一度読む文面を後ろへ続ける（IDENTITY や既定値で
    /// サーバーが決めた値を画面へ写すため。SSMS も足した行をその場で読み直している）。
    /// 読み直す条件を組めないときは INSERT だけになる。
    /// </summary>
    public static ParameterizedStatement RowInsert(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowInsert insert)
    {
        ArgumentNullException.ThrowIfNull(insert);

        var values = new List<object?>();
        var names = new List<string>(insert.Values.Count);
        var placeholders = new List<string>(insert.Values.Count);

        foreach (var cell in insert.Values)
        {
            var column = Find(columns, cell.Column);

            if (column.IsReadOnly)
            {
                throw new TableEditRejectedException($"{column.Name} は値を指定できない列です。");
            }

            names.Add(Quote(column.Name));
            placeholders.Add(ParameterizedStatement.NameOf(values.Count));
            values.Add(ToParameter(column, cell.Value));
        }

        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"INSERT INTO {Quote(schema.Value)}.{Quote(table)}")
            .Append(CultureInfo.InvariantCulture, $" ({string.Join(", ", names)})")
            .Append(CultureInfo.InvariantCulture, $" VALUES ({string.Join(", ", placeholders)});");

        AppendReadback(text, schema, table, columns, insert, values);

        return new ParameterizedStatement(text.ToString(), values);
    }

    /// <summary>行 1 つを消す文面。条件の組み方は <see cref="CellUpdate"/> と同じ。</summary>
    public static ParameterizedStatement RowDelete(
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowDelete delete)
    {
        ArgumentNullException.ThrowIfNull(delete);

        var values = new List<object?>();
        var conditions = Conditions(columns, delete.Criteria, values);

        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"DELETE FROM {Quote(schema.Value)}.{Quote(table)}")
            .Append(CultureInfo.InvariantCulture, $" WHERE {string.Join(" AND ", conditions)};");

        return new ParameterizedStatement(text.ToString(), values);
    }

    /// <summary>
    /// 行を特定する条件。値はパラメータへ逃がし、NULL は <c>IS NULL</c> で比べる
    /// （<c>= NULL</c> は常に不定になり、どの行にも当たらない）。
    /// </summary>
    private static IReadOnlyList<string> Conditions(
        IReadOnlyList<EditableColumn> columns,
        IReadOnlyList<RowCriterion> criteria,
        List<object?> values)
    {
        var conditions = new List<string>(criteria.Count);

        foreach (var criterion in criteria)
        {
            var column = Find(columns, criterion.Column);

            if (criterion.Value is null)
            {
                conditions.Add($"{Quote(column.Name)} IS NULL");
                continue;
            }

            conditions.Add($"{Quote(column.Name)} = {ParameterizedStatement.NameOf(values.Count)}");
            values.Add(ToParameter(column, criterion.Value));
        }

        return conditions;
    }

    /// <summary>
    /// 足した行を読み直す文面を後ろへ続ける。
    ///
    /// 鍵の当て方は列ごとに変わる。採番される列（IDENTITY）は <c>SCOPE_IDENTITY()</c> で、
    /// それ以外は打ち込まれた値で当てる。既定値で決まる鍵（<c>newid()</c> など）は
    /// どちらでも当てられないので、そのときは読み直しを付けない。
    /// </summary>
    private static void AppendReadback(
        StringBuilder text,
        SchemaName schema,
        string table,
        IReadOnlyList<EditableColumn> columns,
        TableRowInsert insert,
        List<object?> values)
    {
        var keys = columns.Where(column => column.IsKey).ToList();

        if (keys.Count == 0)
        {
            return;
        }

        var conditions = new List<string>(keys.Count);
        var readbackValues = new List<object?>();

        foreach (var key in keys)
        {
            if (key.IsIdentity)
            {
                conditions.Add($"{Quote(key.Name)} = SCOPE_IDENTITY()");
                continue;
            }

            var assigned = insert.Values
                .FirstOrDefault(value => string.Equals(value.Column, key.Name, StringComparison.Ordinal));

            if (assigned is null)
            {
                // 何が入ったのかサーバーにしか分からない鍵。読み直しは諦める。
                return;
            }

            if (assigned.Value is null)
            {
                conditions.Add($"{Quote(key.Name)} IS NULL");
                continue;
            }

            conditions.Add($"{Quote(key.Name)} = {ParameterizedStatement.NameOf(values.Count + readbackValues.Count)}");
            readbackValues.Add(ToParameter(key, assigned.Value));
        }

        var names = string.Join(", ", columns.Select(column => Quote(column.Name)));

        text.Append(CultureInfo.InvariantCulture, $" SELECT TOP (1) {names}")
            .Append(CultureInfo.InvariantCulture, $" FROM {Quote(schema.Value)}.{Quote(table)}")
            .Append(CultureInfo.InvariantCulture, $" WHERE {string.Join(" AND ", conditions)};");

        values.AddRange(readbackValues);
    }

    private static EditableColumn Find(IReadOnlyList<EditableColumn> columns, string name) =>
        columns.FirstOrDefault(column => string.Equals(column.Name, name, StringComparison.Ordinal))
        ?? throw new TableEditRejectedException($"{name} 列が見つかりません。テーブルの定義が変わった可能性があります。");

    private static object? ToParameter(EditableColumn column, string? value) =>
        SqlServerCellValue.ToParameter(BaseTypeOf(column), column.DataType, value);

    /// <summary>
    /// 表示用の型名から基本型を取り出す（nvarchar(50) → nvarchar）。
    /// 表示名は <see cref="SqlServerTypeFormat.Describe"/> が基本型から組んだものなので、
    /// 括弧の前がそのまま基本型になる。
    /// </summary>
    private static string BaseTypeOf(EditableColumn column)
    {
        var separator = column.DataType.IndexOf('(', StringComparison.Ordinal);

        return separator < 0 ? column.DataType : column.DataType[..separator];
    }

    private static string Quote(string identifier) => SqlServerIdentifier.Quote(identifier);
}
