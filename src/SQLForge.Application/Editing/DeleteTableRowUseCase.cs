using SQLForge.Application.Abstractions;
using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// 編集グリッドの行 1 つをサーバーから消す。
///
/// SSMS と同じで、確認を取ってから 1 行ずつ DELETE を投げる。条件は鍵になる列
/// （<see cref="Domain.Editing.EditableColumn.IsKey"/>。主キーがあればその列、無ければ
/// 比較できる列すべてで、どれを鍵にするかはドライバーが決めている）の値で組み、
/// ちょうど 1 行に当たらない削除は成立させない。
/// 確認を取るのは画面側の受け持ちで、ここへ来た時点で「消してよい」ことは決まっている。
/// </summary>
public sealed class DeleteTableRowUseCase
{
    public async Task<int> ExecuteAsync(
        IDatabaseSession session,
        DeleteTableRowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var delete = new TableRowDelete(RowCriteria.From(request.Columns, request.Row));

        var affected = await session
            .DeleteTableRowAsync(request.Database, request.Schema, request.Table, delete, cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
        {
            // 条件に当たらなかった。ほかで消されたか、書き換えられたか。
            throw new TableEditRejectedException(
                "対象の行が見つかりませんでした。ほかで変更または削除された可能性があります。読み直してください。");
        }

        return affected;
    }
}
