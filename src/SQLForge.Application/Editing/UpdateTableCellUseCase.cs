using SQLForge.Application.Abstractions;
using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// 編集グリッドのセル 1 つをサーバーへ書き戻す。
///
/// SSMS と同じで、確定するたびに 1 セルずつ UPDATE を投げる。行を特定する条件は
/// 主キー（無ければ比較できる列すべて）の変更前の値で組み、
/// ちょうど 1 行に当たらない更新は成立させない。
/// </summary>
public sealed class UpdateTableCellUseCase
{
    public async Task<int> ExecuteAsync(
        IDatabaseSession session,
        TableCellEditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var column = Target(request);
        var update = new TableCellUpdate(
            column.Name, request.NewValue, RowCriteria.From(request.Columns, request.Row));

        var affected = await session
            .UpdateTableCellAsync(request.Database, request.Schema, request.Table, update, cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
        {
            // 条件に当たらなかった。ほかで書き換えられたか、消えたか。
            throw new TableEditRejectedException(
                "対象の行が見つかりませんでした。ほかで変更または削除された可能性があります。読み直してください。");
        }

        return affected;
    }

    /// <summary>書き換える列を取り出しつつ、そもそも書き換えてよい列かを見る。</summary>
    private static EditableColumn Target(TableCellEditRequest request)
    {
        RowCriteria.EnsureShape(request.Columns, request.Row);

        if (request.Ordinal < 0 || request.Ordinal >= request.Columns.Count)
        {
            throw new TableEditRejectedException("書き換える列が見つかりません。読み直してください。");
        }

        var column = request.Columns[request.Ordinal];

        if (column.IsReadOnly)
        {
            throw new TableEditRejectedException(
                $"{column.Name} は編集できない列です（IDENTITY・計算列・グリッドで扱えない型）。");
        }

        if (request.NewValue is null && !column.IsNullable)
        {
            throw new TableEditRejectedException($"{column.Name} は NULL を許可していない列です。");
        }

        return column;
    }
}
