namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続ダイアログの「参照…」が開くファイル選択。秘密鍵とサーバー証明書の置き場所を選ぶ。
/// 実装は <see cref="Views.SavedConnectionDialogService"/>（親ウィンドウを持っている側）。
/// </summary>
public interface IConnectionFilePrompt
{
    /// <summary>ファイルを 1 つ選ばせる。選ばずに閉じたときは null。</summary>
    Task<string?> AskFileAsync(string title);
}
