using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>操作の結果。接続ダイアログのステータス行にそのまま出す。</summary>
public sealed record SavedConnectionOutcome(bool Succeeded, string Headline, string Detail);

/// <summary>
/// 左ペインの削除・書き出し・取り込み（SSMS の「登録済みサーバー」と同じ扱い）。
///
/// 尋ねるのは <see cref="ISavedConnectionPrompt"/> の受け持ちで、ここは
/// 「尋ねた答えをユースケースへ渡し、一覧を読み直し、結果を伝える」だけを組み立てる。
/// </summary>
public sealed partial class SavedConnectionsViewModel
{
    /// <summary>削除・書き出し・取り込みの結末を伝える。呼び出し側でステータス表示に落とす。</summary>
    public event EventHandler<SavedConnectionOutcome>? OperationCompleted;

    /// <summary>右クリックの「削除」。取り消せないので必ず一度尋ねる。</summary>
    public Task DeleteAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return TryAsync("削除できません", async () =>
        {
            if (!await _prompt.ConfirmDeleteAsync(profile).ConfigureAwait(true))
            {
                return;
            }

            await _deleteConnection.ExecuteAsync(profile.Id, cancellationToken).ConfigureAwait(true);

            CancelSearchReload();
            await LoadAsync(cancellationToken).ConfigureAwait(true);

            Report(true, "削除しました", $"{profile.Name} を保存済み接続から削除しました。");
        });
    }

    /// <summary>右クリックの「書き出し…」。押された 1 件だけを書き出す。</summary>
    public Task ExportAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return ExportAsync(profile.Name, [profile.Id], FileNameFor(profile.Name), cancellationToken);
    }

    /// <summary>フッターの「書き出し」。保存済みのすべてを 1 枚にまとめる。</summary>
    [RelayCommand]
    public Task ExportAllAsync(CancellationToken cancellationToken = default) =>
        ExportAsync("すべての保存済み接続", [], "connections.toml", cancellationToken);

    /// <summary>フッターの「取り込み」。手元の接続に当たったものは 1 件ずつ尋ねる。</summary>
    [RelayCommand]
    public Task ImportAsync(CancellationToken cancellationToken = default) =>
        TryAsync("取り込めません", async () =>
        {
            if (await _prompt.AskImportFileAsync().ConfigureAwait(true) is not { } path)
            {
                return;
            }

            var candidates = await _importConnections.ReadAsync(path, cancellationToken).ConfigureAwait(true);
            if (await ResolveConflictsAsync(candidates).ConfigureAwait(true) is not { } accepted)
            {
                return;
            }

            var imported = await _importConnections.ApplyAsync(accepted, cancellationToken).ConfigureAwait(true);

            CancelSearchReload();
            await LoadAsync(cancellationToken).ConfigureAwait(true);

            Report(true, "取り込みました", Describe(imported, candidates.Count - imported));
        });

    private Task ExportAsync(
        string target,
        IReadOnlyCollection<ConnectionProfileId> ids,
        string suggestedFileName,
        CancellationToken cancellationToken) =>
        TryAsync("書き出せません", async () =>
        {
            if (await _prompt.AskExportAsync(target, suggestedFileName).ConfigureAwait(true) is not { } choice)
            {
                return;
            }

            var exported = await _exportConnections
                .ExecuteAsync(choice.Path, ids, choice.IncludeCredentials, cancellationToken)
                .ConfigureAwait(true);

            Report(true, "書き出しました", $"{exported} 件を {choice.Path} に書き出しました。");
        });

    /// <summary>
    /// 手元の接続に当たったものを 1 件ずつ尋ねる。「すべて」を選ばれたら以降は尋ねない。
    /// やめると言われたら null を返し、1 件も書かずに終わる。
    /// </summary>
    private async Task<List<ConnectionImportCandidate>?> ResolveConflictsAsync(
        IReadOnlyList<ConnectionImportCandidate> candidates)
    {
        var accepted = new List<ConnectionImportCandidate>();
        var replaceAll = false;
        var skipAll = false;

        foreach (var candidate in candidates)
        {
            if (candidate.Existing is not { } existing)
            {
                accepted.Add(candidate);
                continue;
            }

            if (skipAll)
            {
                continue;
            }

            var choice = replaceAll
                ? ImportConflictChoice.ReplaceAll
                : await _prompt.AskConflictAsync(existing).ConfigureAwait(true);

            if (choice is ImportConflictChoice.Cancel)
            {
                return null;
            }

            replaceAll |= choice is ImportConflictChoice.ReplaceAll;
            skipAll |= choice is ImportConflictChoice.SkipAll;

            if (choice is ImportConflictChoice.Replace or ImportConflictChoice.ReplaceAll)
            {
                accepted.Add(candidate);
            }
        }

        return accepted;
    }

    private static string Describe(int imported, int skipped) =>
        skipped == 0
            ? $"{imported} 件を保存済み接続に足しました。"
            : $"{imported} 件を保存済み接続に足しました（{skipped} 件は飛ばしました）。";

    /// <summary>接続名をそのままファイル名にすると、区切り文字で置き場所が変わってしまう。</summary>
    private static string FileNameFor(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())) + ".toml";

    /// <summary>
    /// 尋ねるところも含めて包む。ファイル ダイアログを出せない環境では
    /// 尋ねる側が投げてくるので、そこもダイアログを閉じずに理由を出したい。
    /// </summary>
    private async Task TryAsync(string failureHeadline, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // ダイアログごと閉じられた。伝える相手がもういない。
        }
        catch (Exception exception)
        {
            // 読めないファイル・書けない置き場所・キーリングの不調はここへ来る。
            Report(false, failureHeadline, exception.Message);
        }
    }

    private void Report(bool succeeded, string headline, string detail) =>
        OperationCompleted?.Invoke(this, new SavedConnectionOutcome(succeeded, headline, detail));
}
