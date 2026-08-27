using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 取り込みで手元の接続に当たったときの選び方。
/// SSMS の取り込みと同じ 5 択（キャンセルを 0 にしてあるのは、窓の × で閉じたときに
/// 既定値がキャンセルになるようにするため）。
/// </summary>
public enum ImportConflictChoice
{
    /// <summary>取り込みそのものをやめる。</summary>
    Cancel,

    /// <summary>この 1 件を置き換える。</summary>
    Replace,

    /// <summary>以降の当たりもすべて置き換える。</summary>
    ReplaceAll,

    /// <summary>この 1 件を飛ばす。</summary>
    Skip,

    /// <summary>以降の当たりもすべて飛ばす。</summary>
    SkipAll
}

/// <summary>書き出しの条件。SSMS の「登録済みサーバーのエクスポート」で決めるもの。</summary>
public sealed record ConnectionExportChoice(string Path, bool IncludeCredentials);

/// <summary>
/// 保存済み接続の削除・書き出し・取り込みで、利用者に尋ねる口。
///
/// ダイアログとファイル選択を出すのはビューの受け持ちなので、
/// ビューモデルはこの口だけを知る（<see cref="Workspace.IRowDeletionPrompt"/> と同じ形）。
/// </summary>
public interface ISavedConnectionPrompt
{
    /// <summary>消してよいか尋ねる。押されたら true。</summary>
    Task<bool> ConfirmDeleteAsync(ConnectionProfile profile);

    /// <summary>書き出し先と、ユーザー名とパスワードを含めるかを尋ねる。やめたら null。</summary>
    /// <param name="target">書き出す対象の言い方（接続名、または「すべての保存済み接続」）。</param>
    /// <param name="suggestedFileName">既定のファイル名。</param>
    Task<ConnectionExportChoice?> AskExportAsync(string target, string suggestedFileName);

    /// <summary>取り込むファイルを選んでもらう。やめたら null。</summary>
    Task<string?> AskImportFileAsync();

    /// <summary>手元の接続に当たったことを伝え、どうするか尋ねる。</summary>
    Task<ImportConflictChoice> AskConflictAsync(ConnectionProfile existing);
}
