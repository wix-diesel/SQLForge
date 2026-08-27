using Avalonia.Platform.Storage;
using SQLForge.Domain.Connections;
using SQLForge.Ui.ViewModels;

namespace SQLForge.Ui.Views;

/// <summary>
/// 左ペインの削除・書き出し・取り込みで出すダイアログ（<see cref="ISavedConnectionPrompt"/> の実装）。
/// モーダルの出し方は <see cref="SecurityDialogService"/> から借りる。
/// 親ウィンドウは接続ダイアログで、開いたところ（App）で <see cref="SecurityDialogService.Owner"/> に差す。
///
/// 入力欄の「参照…」（<see cref="IConnectionFilePrompt"/>）も、同じ親ウィンドウと
/// ファイル選択の仕組みを使うのでここが受け持つ。
/// </summary>
public sealed class SavedConnectionDialogService : SecurityDialogService, ISavedConnectionPrompt, IConnectionFilePrompt
{
    public Task<bool> ConfirmDeleteAsync(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return ShowAsync(ConfirmDialogViewModel.Destructive(
            "接続の削除",
            $"{profile.Name} を削除します。",
            "保存した接続情報と、預けてあるパスワードを消します。元に戻せません。続行しますか？",
            "削除"));
    }

    public async Task<ConnectionExportChoice?> AskExportAsync(string target, string suggestedFileName)
    {
        var dialog = new ConnectionExportDialogViewModel(target, DefaultPathFor(suggestedFileName));
        var window = new ConnectionExportWindow { DataContext = dialog };
        dialog.CloseRequested += (_, result) => window.Close(result);

        return await ShowAsync(window).ConfigureAwait(true)
            ? new ConnectionExportChoice(dialog.FilePath.Trim(), !dialog.ExcludeCredentials)
            : null;
    }

    public async Task<string?> AskImportFileAsync()
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "接続情報の取り込み",
            AllowMultiple = false,
            FileTypeFilter = [ConnectionArchiveFile.Type]
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    /// <summary>秘密鍵・サーバー証明書を選ぶ。種別で絞らないのは、拡張子が定まらないため。</summary>
    public async Task<string?> AskFileAsync(string title)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public Task<ImportConflictChoice> AskConflictAsync(ConnectionProfile existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var dialog = new ImportConflictDialogViewModel(existing);
        var window = new ImportConflictWindow { DataContext = dialog };
        dialog.CloseRequested += (_, choice) => window.Close(choice);

        return Owner is { } owner
            ? window.ShowDialog<ImportConflictChoice>(owner)
            : throw new InvalidOperationException("ダイアログの親ウィンドウが決まっていません。");
    }

    private IStorageProvider Storage =>
        Owner?.StorageProvider ?? throw new InvalidOperationException("ダイアログの親ウィンドウが決まっていません。");

    /// <summary>置き場所の既定はホーム。設定ディレクトリへ書き出しても持ち運べないため。</summary>
    private static string DefaultPathFor(string fileName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fileName);
}
