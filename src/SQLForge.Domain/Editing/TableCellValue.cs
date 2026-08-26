namespace SQLForge.Domain.Editing;

/// <summary>
/// 行を足すときに置く値 1 つ。値は画面に出ているままの文字列で、null は SQL の NULL を表す。
///
/// 「触っていない列」はここに現れない。触っていない列を NULL として送ると、
/// 既定値（DEFAULT 制約）が効かなくなるため（SSMS も、打ち込まれた列だけを INSERT に並べる）。
/// </summary>
/// <param name="Column">値を置く列名。</param>
/// <param name="Value">置く値。</param>
public sealed record TableCellValue(string Column, string? Value);
