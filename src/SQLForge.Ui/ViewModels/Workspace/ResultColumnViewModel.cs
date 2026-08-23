using Avalonia.Media;
using SQLForge.Domain.Query;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 結果グリッドの列 1 つ。幅は中身から決めて、見出しのセルとデータのセルが同じ値を引く。
/// </summary>
public sealed class ResultColumnViewModel(QueryColumn column, double width)
{
    public string Name { get; } = column.Name;

    public string TypeName { get; } = column.TypeName;

    public bool IsNumeric { get; } = column.IsNumeric;

    public double Width { get; } = width;

    /// <summary>数値は右寄せ。桁を縦に揃えないと大小が読めないため。</summary>
    public TextAlignment Alignment { get; } = column.IsNumeric ? TextAlignment.Right : TextAlignment.Left;
}
