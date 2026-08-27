using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// 行 1 つを消す入力。
///
/// <paramref name="Row"/> は「サーバーにいま入っているはずの値」で、行を特定する条件に使う
/// （<see cref="TableCellEditRequest"/> と同じ理由で、編集中の文字列ではなく確定済みの値を渡す）。
/// </summary>
/// <param name="Database">実行先のデータベース。</param>
/// <param name="Schema">テーブルのスキーマ。</param>
/// <param name="Table">テーブル名。</param>
/// <param name="Columns">グリッドの列の並び。</param>
/// <param name="Row">消す行の値。<paramref name="Columns"/> と同じ並び。</param>
public sealed record DeleteTableRowRequest(
    DatabaseName Database,
    SchemaName Schema,
    string Table,
    IReadOnlyList<EditableColumn> Columns,
    IReadOnlyList<string?> Row);
