namespace SQLForge.Domain.Editing;

/// <summary>
/// 更新するときに行を特定する条件 1 つ。値は画面に出ているままの文字列で、
/// null は SQL の NULL（つまり <c>IS NULL</c> での比較）を表す。
/// </summary>
/// <param name="Column">条件に使う列名。</param>
/// <param name="Value">変更前の値。</param>
public sealed record RowCriterion(string Column, string? Value);
