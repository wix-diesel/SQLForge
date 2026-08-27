using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 「接続情報の書き出し」ダイアログ。SSMS の「登録済みサーバーのエクスポート」と同じで、
/// 書き出す先と、ユーザー名とパスワードを含めるかどうかを決める。
///
/// 既定は「含めない」。含めると、預けてあるパスワードがそのまま読める形でファイルに載る。
/// </summary>
public sealed partial class ConnectionExportDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _filePath;
    [ObservableProperty] private bool _excludeCredentials = true;

    public ConnectionExportDialogViewModel(string target, string filePath)
    {
        Target = target;
        _filePath = filePath;
    }

    public string Title => "接続情報の書き出し";

    /// <summary>書き出す対象の言い方（接続名、または「すべての保存済み接続」）。</summary>
    public string Target { get; }

    /// <summary>置き場所が決まっていなければ書き出せない。</summary>
    public bool CanExport => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>含めるとファイルに何が載るかの注意書き。切り替えに合わせて出し入れする。</summary>
    public bool WarnsAboutCredentials => !ExcludeCredentials;

    /// <summary>閉じることを伝える。true なら「書き出し」が押された。</summary>
    public event EventHandler<bool>? CloseRequested;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    partial void OnFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(CanExport));
        ExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnExcludeCredentialsChanged(bool value) => OnPropertyChanged(nameof(WarnsAboutCredentials));
}
