using SQLForge.Application.Abstractions;
using SQLForge.Domain.Editing;

namespace SQLForge.Application.Editing;

/// <summary>
/// 編集グリッドのいちばん下の行（行番号が <c>*</c> の行）をサーバーへ足す。
///
/// SSMS と同じで、確定するたびに 1 行ずつ INSERT を投げる。打ち込まれた列だけを並べ、
/// 触っていない列はサーバーの既定値に任せる。
/// </summary>
public sealed class InsertTableRowUseCase
{
    /// <returns>
    /// 足したあとの行の値（列の並びと同じ）。IDENTITY や既定値でサーバーが決めた値を
    /// 画面へ写すのに使う。読み直す条件を組めなかったときは null。
    /// </returns>
    public async Task<IReadOnlyList<string?>?> ExecuteAsync(
        IDatabaseSession session,
        InsertTableRowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var insert = new TableRowInsert(Values(request));

        return await session
            .InsertTableRowAsync(request.Database, request.Schema, request.Table, insert, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>置く値を確かめる。サーバーが決める列と、NULL を許さない列をここで弾く。</summary>
    private static IReadOnlyList<TableCellValue> Values(InsertTableRowRequest request)
    {
        if (request.Columns.Count == 0)
        {
            throw new TableEditRejectedException("列の並びが分かりません。読み直してください。");
        }

        if (request.Values.Count == 0)
        {
            // すべて既定値の行は足さない（SSMS も、何も打ち込まれていない行は送らない）。
            throw new TableEditRejectedException("値が入力されていません。1 つ以上のセルに値を入れてください。");
        }

        foreach (var value in request.Values)
        {
            Ensure(request.Columns, value);
        }

        return request.Values;
    }

    private static void Ensure(IReadOnlyList<EditableColumn> columns, TableCellValue value)
    {
        var column = columns.FirstOrDefault(candidate => string.Equals(candidate.Name, value.Column, StringComparison.Ordinal))
            ?? throw new TableEditRejectedException($"{value.Column} 列が見つかりません。読み直してください。");

        if (column.IsReadOnly)
        {
            throw new TableEditRejectedException(
                $"{column.Name} は値を指定できない列です（IDENTITY・計算列・グリッドで扱えない型）。");
        }

        if (value.Value is null && !column.IsNullable)
        {
            throw new TableEditRejectedException($"{column.Name} は NULL を許可していない列です。");
        }
    }
}
