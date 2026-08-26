using SQLForge.Application.Abstractions;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ログインを 1 件保存する。新規なら作成、編集なら変更として渡す。
/// どちらになるかは下書きが元の姿を持っているかだけで決まる。
///
/// ユーザー マッピングはログインそのものとは別のデータベースに書くので、
/// ログインを保存し終えてから流す（名前を変えた編集でも、新しい名前で対応づく）。
/// </summary>
public sealed class SaveServerLoginUseCase
{
    public async Task<SecurityValidationResult> ExecuteAsync(
        IDatabaseSession session,
        ServerLoginDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);

        var validation = ServerLoginValidator.Validate(draft);

        if (!validation.IsValid)
        {
            return validation;
        }

        var definition = draft.ToDefinition();

        if (draft.Original is { } original)
        {
            await session.AlterServerLoginAsync(original, definition, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await session.CreateServerLoginAsync(definition, cancellationToken).ConfigureAwait(false);
        }

        await ApplyMappingsAsync(session, draft, definition, cancellationToken).ConfigureAwait(false);

        return validation;
    }

    /// <summary>
    /// マッピングを開いていないダイアログ（ページを見なかった編集）では前後とも空になる。
    /// そのときに送ると「すべてのマッピングを外す」になってしまうので、何もしない。
    /// </summary>
    private static Task ApplyMappingsAsync(
        IDatabaseSession session,
        ServerLoginDraft draft,
        ServerLoginDefinition definition,
        CancellationToken cancellationToken)
    {
        var desired = draft.ToMappings();

        if (draft.OriginalMappings.Count == 0 && desired.Count == 0)
        {
            return Task.CompletedTask;
        }

        return session.ApplyLoginUserMappingsAsync(
            definition.Name,
            draft.OriginalMappings,
            desired,
            cancellationToken);
    }
}
