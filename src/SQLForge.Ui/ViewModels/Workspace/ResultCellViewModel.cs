namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// グリッドのセル 1 つ。NULL は文字列ではなく状態として持ち、薄い字で出す
/// （'NULL' という文字が入っているセルと見分けが付かなくなるのを避けるため）。
/// </summary>
public sealed class ResultCellViewModel(ResultColumnViewModel column, string? value)
{
    /// <summary>属する列。幅と寄せ方をここから引く。</summary>
    public ResultColumnViewModel Column { get; } = column;

    public bool IsNull { get; } = value is null;

    public string Text { get; } = value ?? "NULL";
}
