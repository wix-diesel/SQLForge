using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// セル 1 つを書き換える入力。
///
/// <paramref name="Row"/> は「サーバーにいま入っているはずの値」で、行を特定する条件に使う。
/// グリッドの表示ではなく確定済みの値を渡す（編集中の文字列を条件にすると、
/// 書き換えたあとの行が見つからなくなる）。
/// </summary>
/// <param name="Database">実行先のデータベース。</param>
/// <param name="Schema">テーブルのスキーマ。</param>
/// <param name="Table">テーブル名。</param>
/// <param name="Columns">グリッドの列の並び。</param>
/// <param name="Row">変更前の行の値。<paramref name="Columns"/> と同じ並び。</param>
/// <param name="Ordinal">書き換える列の位置（0 始まり）。</param>
/// <param name="NewValue">新しい値。null は SQL の NULL。</param>
public sealed record TableCellEditRequest(
    DatabaseName Database,
    SchemaName Schema,
    string Table,
    IReadOnlyList<EditableColumn> Columns,
    IReadOnlyList<string?> Row,
    int Ordinal,
    string? NewValue);
