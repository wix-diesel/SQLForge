using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Editing;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 編集グリッドの行を足す・消す受け持ち。
///
/// SSMS と同じ手順にする。グリッドのいちばん下には行番号が <c>*</c> の新しい行が常にあり、
/// そこへ打ち込んで確定すると 1 行が足される（触っていない列は既定値に任せる）。
/// 削除は行の右クリックからで、取り消せないので必ず一度尋ねる。
/// </summary>
public sealed partial class TableEditorViewModel
{
    /// <summary>行を足している最中か。確定の途中でまた確定へ入るのを防ぐ。</summary>
    private bool _isInserting;

    /// <summary>いちばん下の新しい行。足せないテーブルでは null。</summary>
    public EditableRowViewModel? NewRow => Rows.FirstOrDefault(row => row.IsNewRow);

    /// <summary>
    /// 新しい行を 1 行として足す。打ち込まれた列だけを並べ、通ったらその行を普通の行にして、
    /// 下に新しい行をまた出す（SSMS と同じ）。
    /// </summary>
    internal async Task CommitNewRowAsync(EditableRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsNewRow || _isInserting || !row.HasPendingValues)
        {
            // 何も打ち込まれていない行は足さない（SSMS も、触っていない行は送らない）。
            return;
        }

        if (_database is not { } database || _schema is not { } schema || _table is not { } table)
        {
            return;
        }

        var generation = _generation;
        _isInserting = true;

        try
        {
            var request = new InsertTableRowRequest(database, schema, table, _definitions, row.AssignedValues);
            var inserted = await _insertRow.ExecuteAsync(_session, request).ConfigureAwait(true);

            if (!IsCurrent(generation))
            {
                return;
            }

            if (inserted is null)
            {
                // サーバーが決めた値を当てにいけない鍵（既定値で決まる主キーなど）。
                // 画面と中身が食い違わないように、読み直して実際の姿を出す。
                await LoadAsync(CancellationToken.None).ConfigureAwait(true);

                // 読み直しそのものが落ちたときは、その理由を残す。
                if (!HasFailed)
                {
                    Announce("1 行を追加しました。実際の値を出すために読み直しました。");
                }

                return;
            }

            var number = Rows.Count(candidate => !candidate.IsNewRow) + 1;

            row.Apply(inserted);
            row.MarkCommitted(number);
            AppendNewRow();
            Announce("1 行を追加しました。");
        }
        catch (Exception exception)
        {
            // 制約違反や権限不足はここへ来る。打ちかけは残したまま理由だけを出す
            // （直してもう一度確定できるように。SSMS と同じ）。
            if (IsCurrent(generation))
            {
                ShowFailure(exception.Message);
            }
        }
        finally
        {
            _isInserting = false;
        }
    }

    /// <summary>
    /// 行を 1 つ消す。まだサーバーに無い新しい行は、打ちかけを捨てるだけで済ませる。
    /// </summary>
    internal async Task DeleteRowAsync(EditableRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.IsNewRow)
        {
            CancelNewRow();

            return;
        }

        if (!CanDelete || _database is not { } database || _schema is not { } schema || _table is not { } table)
        {
            return;
        }

        var generation = _generation;

        if (!await _deletionPrompt.ConfirmDeleteAsync(1).ConfigureAwait(true) || !IsCurrent(generation))
        {
            // やめた、あるいは尋ねている間に読み直された。読み直されていれば行はもう別物。
            return;
        }

        try
        {
            var request = new DeleteTableRowRequest(database, schema, table, _definitions, row.Values);
            await _deleteRow.ExecuteAsync(_session, request).ConfigureAwait(true);

            if (!IsCurrent(generation))
            {
                return;
            }

            Rows.Remove(row);
            Renumber();
            Announce("1 行を削除しました。");
        }
        catch (Exception exception)
        {
            if (IsCurrent(generation))
            {
                ShowFailure(exception.Message);
            }
        }
    }

    /// <summary>
    /// Esc と右クリックの「新しい行を取り消す」。打ちかけを捨てる。
    /// 行の側（<see cref="EditableRowViewModel"/>）からもここを通す。一行に出す文面まで
    /// 含めて取り消しなので、行が自分で <see cref="EditableRowViewModel.Reset"/> だけを
    /// 呼ぶと、画面には「編集中です」が残ってしまう。
    /// </summary>
    [RelayCommand]
    internal void CancelNewRow()
    {
        if (NewRow is not { HasPendingValues: true } pending)
        {
            return;
        }

        pending.Reset();
        Announce("新しい行を取り消しました。");
    }

    /// <summary>
    /// ほかの行へ移った。打ちかけの新しい行があれば、そこで確定する（SSMS と同じ）。
    /// </summary>
    internal void LeaveRow(EditableRowViewModel row)
    {
        if (_isInserting || NewRow is not { HasPendingValues: true } pending || ReferenceEquals(pending, row))
        {
            return;
        }

        // 押した先のセルを開くのは呼び出し側の続きなので、こちらは投げっぱなしにする
        // （CommitNewRowAsync が例外を受け止める）。
        _ = CommitNewRowAsync(pending);
    }

    /// <summary>新しい行に値が置かれた。確定と取り消しのしかたを一行に出す。</summary>
    internal void NotifyRowPending() => Announce("新しい行を編集中です。Enter で追加、Esc で取り消します。");

    /// <summary>行を足せるなら、いちばん下に新しい行を出す。</summary>
    private void AppendNewRow()
    {
        if (CanInsert)
        {
            Rows.Add(new EditableRowViewModel(this, Rows.Count + 1, Columns, [], isNewRow: true));
        }
    }

    /// <summary>行番号を振り直す（行を消したあと）。新しい行は <c>*</c> のまま。</summary>
    private void Renumber()
    {
        var number = 0;

        foreach (var row in Rows)
        {
            if (!row.IsNewRow)
            {
                row.Renumber(++number);
            }
        }
    }

    /// <summary>うまくいったことを一行に出す。失敗の印は落とす。</summary>
    private void Announce(string message)
    {
        Status = message;
        HasFailed = false;
    }
}
