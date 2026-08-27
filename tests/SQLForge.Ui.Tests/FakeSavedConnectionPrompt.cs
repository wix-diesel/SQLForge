using SQLForge.Domain.Connections;
using SQLForge.Ui.ViewModels;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 左ペインの尋ね事（削除の確認・書き出し先・取り込むファイル・当たったときの選択）の差し替え。
/// 答えを決め打ちにして、ダイアログを出さずにビューモデルの筋を確かめる。
/// </summary>
public sealed class FakeSavedConnectionPrompt : ISavedConnectionPrompt
{
    /// <summary>削除の確認で押される答え。</summary>
    public bool ConfirmsDelete { get; set; } = true;

    /// <summary>書き出しダイアログの答え。null なら「やめた」。</summary>
    public ConnectionExportChoice? ExportChoice { get; set; }

    /// <summary>取り込むファイル。null なら「やめた」。</summary>
    public string? ImportFile { get; set; }

    /// <summary>当たったものへの答えを、尋ねられる順に並べておく。</summary>
    public Queue<ImportConflictChoice> ConflictAnswers { get; } = new();

    public List<ConnectionProfile> DeleteRequests { get; } = [];

    public List<string> ExportTargets { get; } = [];

    public List<ConnectionProfile> ConflictRequests { get; } = [];

    /// <summary>書き出しダイアログへ渡された既定のファイル名。</summary>
    public string? SuggestedFileName { get; private set; }

    public Task<bool> ConfirmDeleteAsync(ConnectionProfile profile)
    {
        DeleteRequests.Add(profile);

        return Task.FromResult(ConfirmsDelete);
    }

    public Task<ConnectionExportChoice?> AskExportAsync(string target, string suggestedFileName)
    {
        ExportTargets.Add(target);
        SuggestedFileName = suggestedFileName;

        return Task.FromResult(ExportChoice);
    }

    public Task<string?> AskImportFileAsync() => Task.FromResult(ImportFile);

    public Task<ImportConflictChoice> AskConflictAsync(ConnectionProfile existing)
    {
        ConflictRequests.Add(existing);

        return Task.FromResult(ConflictAnswers.Count > 0 ? ConflictAnswers.Dequeue() : ImportConflictChoice.Cancel);
    }
}
