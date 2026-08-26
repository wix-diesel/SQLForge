using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// 行 1 つを足す入力。
///
/// <paramref name="Values"/> には打ち込まれた列だけを並べる。触っていない列を並べると、
/// 既定値（DEFAULT 制約）が効かなくなる。
/// </summary>
/// <param name="Database">実行先のデータベース。</param>
/// <param name="Schema">テーブルのスキーマ。</param>
/// <param name="Table">テーブル名。</param>
/// <param name="Columns">グリッドの列の並び。</param>
/// <param name="Values">打ち込まれた値。列の並びに現れる列だけを指せる。</param>
public sealed record InsertTableRowRequest(
    DatabaseName Database,
    SchemaName Schema,
    string Table,
    IReadOnlyList<EditableColumn> Columns,
    IReadOnlyList<TableCellValue> Values);
