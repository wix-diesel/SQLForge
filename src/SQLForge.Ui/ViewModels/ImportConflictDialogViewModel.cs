using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 取り込もうとした接続が手元にもあったときの確認。
/// SSMS の取り込みと同じ 5 択（はい / すべてはい / いいえ / すべていいえ / キャンセル）。
/// </summary>
public sealed partial class ImportConflictDialogViewModel(ConnectionProfile existing)
{
    private readonly ConnectionProfile _existing = existing;

    public string Title => "接続情報の取り込み";

    public string Headline => $"{_existing.Name} は既にあります。";

    public string Detail => "取り込むほうで置き換えますか？　置き換えると、今の接続情報と預けてあるパスワードは元に戻せません。";

    /// <summary>どれを指しているのかが分かるよう、手元の接続の繋ぎ先も出す。</summary>
    public string Summary => $"{_existing.Environment.DisplayName} · {_existing.Target.Summary}";

    /// <summary>閉じることを伝える。</summary>
    public event EventHandler<ImportConflictChoice>? CloseRequested;

    [RelayCommand]
    private void Replace() => Close(ImportConflictChoice.Replace);

    [RelayCommand]
    private void ReplaceAll() => Close(ImportConflictChoice.ReplaceAll);

    [RelayCommand]
    private void Skip() => Close(ImportConflictChoice.Skip);

    [RelayCommand]
    private void SkipAll() => Close(ImportConflictChoice.SkipAll);

    [RelayCommand]
    private void Cancel() => Close(ImportConflictChoice.Cancel);

    private void Close(ImportConflictChoice choice) => CloseRequested?.Invoke(this, choice);
}
