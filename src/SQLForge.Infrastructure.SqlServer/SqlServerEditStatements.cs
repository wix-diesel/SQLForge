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
        var conditions = new List<string>(update.Criteria.Count);

        foreach (var criterion in update.Criteria)
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

        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"UPDATE {Quote(schema.Value)}.{Quote(table)}")
            .Append(CultureInfo.InvariantCulture, $" SET {Quote(target.Name)} = {ParameterizedStatement.NameOf(0)}")
            .Append(CultureInfo.InvariantCulture, $" WHERE {string.Join(" AND ", conditions)};");

        return new ParameterizedStatement(text.ToString(), values);
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
