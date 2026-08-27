using SQLForge.Ui.ViewModels.Workspace;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 行を消す前の確認。押された答えを決め打ちにして、尋ねられた回数を残す。
/// 「確認を取らずに消していないか」を見るのが、ここでのいちばんの関心事。
/// </summary>
internal sealed class FakeRowDeletionPrompt(bool answer = true) : IRowDeletionPrompt
{
    /// <summary>尋ねられた回数。</summary>
    public int Calls { get; private set; }

    /// <summary>尋ねられた行数。</summary>
    public int LastRowCount { get; private set; }

    public Task<bool> ConfirmDeleteAsync(int rowCount)
    {
        Calls++;
        LastRowCount = rowCount;

        return Task.FromResult(answer);
    }
}
