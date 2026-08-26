using Avalonia.Media;
using SQLForge.Domain.Editing;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 編集グリッドの列 1 つ。幅の決め方は結果グリッド（<see cref="ResultColumnViewModel"/>）と同じで、
/// 見出しのセルとデータのセルが同じ値を引く。
/// </summary>
/// <param name="column">列の素性。</param>
/// <param name="width">列幅。</param>
/// <param name="canEdit">グリッド全体が編集できる状態か。読み取り専用の接続では false。</param>
public sealed class EditableColumnViewModel(EditableColumn column, double width, bool canEdit)
{
    public string Name { get; } = column.Name;

    public string TypeName { get; } = column.DataType;

    /// <summary>行を特定する条件に使う列。見出しに鍵を出す。</summary>
    public bool IsKey { get; } = column.IsKey;

    /// <summary>この列のセルを書き換えられるか。</summary>
    public bool IsEditable { get; } = canEdit && !column.IsReadOnly;

    /// <summary>文字列の列か。空欄の確定を空文字列と NULL のどちらにするかを分ける。</summary>
    public bool IsText { get; } = column.IsText;

    public double Width { get; } = width;

    /// <summary>数値は右寄せ（結果グリッドと同じ理由）。</summary>
    public TextAlignment Alignment { get; } = column.IsNumeric ? TextAlignment.Right : TextAlignment.Left;
}
