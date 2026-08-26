using SQLForge.Application.Editing;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Editing;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.ViewModels.Workspace;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 編集グリッドのふるまい。ツリーから開く → セルを開く → 打ち直す → 確定、までを追う。
///
/// SSMS の編集グリッドと同じで、確定するたびにその 1 セルだけを書き戻す。
/// 通らなかったときに画面だけが新しい値になっていないことが、ここでのいちばんの関心事。
/// </summary>
public class TableEditorViewModelTests
{
    private static readonly DatabaseName ShopDb = new("shop");
    private static readonly SchemaName Dbo = new("dbo");

    [Fact]
    public async Task ツリーから開くと先頭100行を読む()
    {
        var session = ReadWriteSession();
        var editor = Editor(session);

        Assert.False(editor.IsOpen);

        editor.OpenTableEditor(ShopDb, Dbo, "orders");
        await Settle(editor);

        Assert.True(editor.IsOpen);
        Assert.Equal("dbo.orders", editor.Title);
        Assert.Equal("shop", editor.TargetDatabase);
        Assert.Equal(100, session.EditableMaxRows);
        Assert.Equal(["id", "customer", "status", "amount"], editor.Columns.Select(column => column.Name));

        // 2 行と、いちばん下の新しい行（SSMS と同じで、行を足せるときは常に出る）。
        Assert.Equal(3, editor.Rows.Count);
        Assert.Equal(["1", "2", "*"], editor.Rows.Select(row => row.Number));
    }

    [Fact]
    public async Task セルを確定すると変わった1セルだけを書き戻す()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        var cell = editor.Rows[0].Cells[2];
        cell.BeginEdit();
        cell.EditText = "shipped";
        await cell.CommitAsync();

        Assert.Equal("shop.dbo.orders", session.UpdatedTable);
        Assert.Equal("status", session.LastUpdate!.Column);
        Assert.Equal("shipped", session.LastUpdate.Value);
        Assert.Equal([("id", "1")], session.LastUpdate.Criteria.Select(c => (c.Column, c.Value)));

        // 通ったので、表示もその値になる。
        Assert.Equal("shipped", cell.Value);
        Assert.False(cell.HasError);
        Assert.False(editor.HasFailed);
    }

    [Fact]
    public async Task 値が変わっていなければ書き戻さない()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        var cell = editor.Rows[0].Cells[2];
        cell.BeginEdit();
        await cell.CommitAsync();

        Assert.Null(session.LastUpdate);
    }

    [Fact]
    public async Task Escで元の値へ戻す()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        var cell = editor.Rows[0].Cells[2];
        cell.BeginEdit();
        cell.EditText = "打ちかけ";
        cell.CancelEdit();

        Assert.False(cell.IsEditing);
        Assert.Equal("paid", cell.Value);
        Assert.Null(session.LastUpdate);
    }

    [Fact]
    public async Task 落ちたときは表示を元のままにして理由を出す()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);
        session.EditFailure = new InvalidOperationException("UPDATE ステートメントが競合しました。");

        var cell = editor.Rows[0].Cells[2];
        cell.BeginEdit();
        cell.EditText = "shipped";
        await cell.CommitAsync();

        // 画面だけが新しい値になっていると、サーバーの中身と食い違ったままになる。
        Assert.Equal("paid", cell.Value);
        Assert.True(cell.HasError);
        Assert.True(editor.HasFailed);
        Assert.Contains("競合", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 空欄は文字列の列なら空文字列それ以外はNULLになる()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        // status は nvarchar。空欄はそのまま空文字列。
        var text = editor.Rows[0].Cells[2];
        text.BeginEdit();
        text.EditText = string.Empty;
        await text.CommitAsync();

        Assert.Equal(string.Empty, session.LastUpdate!.Value);

        // amount は decimal。空欄は NULL とする（値が消えたのか空文字なのかを型で決める）。
        var number = editor.Rows[0].Cells[3];
        number.BeginEdit();
        number.EditText = string.Empty;
        await number.CommitAsync();

        Assert.Null(session.LastUpdate.Value);
    }

    [Fact]
    public async Task NULLを入れる操作は別に用意する()
    {
        // 空欄と NULL は見分けが付かないので、SSMS と同じく Ctrl+0（と右クリック）だけで入れる。
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        await editor.Rows[0].Cells[2].SetNullAsync();

        Assert.Null(session.LastUpdate!.Value);
        Assert.True(editor.Rows[0].Cells[2].IsNull);
        Assert.Equal("NULL", editor.Rows[0].Cells[2].Text);
    }

    [Fact]
    public async Task 書き換えられない列のセルは開かない()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        // id は IDENTITY。
        var cell = editor.Rows[0].Cells[0];
        cell.BeginEdit();

        Assert.False(cell.IsEditable);
        Assert.False(cell.IsEditing);
    }

    [Fact]
    public async Task 読み取り専用の接続では書き換えられない()
    {
        // 見本データの先頭は本番タグの読み取り専用接続。読むだけで開き、理由を添える。
        var session = new FakeDatabaseSession().WithEditableRows("shop", "dbo", "orders", Rows());
        Assert.True(session.Profile.IsReadOnly);

        var editor = await OpenAsync(session);

        Assert.False(editor.CanEdit);
        Assert.All(editor.Rows[0].Cells, cell => Assert.False(cell.IsEditable));
        Assert.Contains("読み取り専用", editor.ReadOnlyReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 主キーが無いテーブルは読むだけにする()
    {
        var session = ReadWriteSession();
        session.WithEditableRows(
            "shop",
            "dbo",
            "notes",
            new EditableRowSet(
                [
                    new EditableColumn(
                        "body", "ntext", IsNullable: true, IsKey: false, IsReadOnly: true, IsNumeric: false, IsText: false)
                ],
                [new string?[] { "memo" }]));

        var editor = Editor(session);
        editor.OpenTableEditor(ShopDb, Dbo, "notes");
        await Settle(editor);

        Assert.False(editor.CanEdit);
        Assert.Contains("主キー", editor.ReadOnlyReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 読み込みに失敗したら理由を出す()
    {
        var session = ReadWriteSession();
        session.EditFailure = new InvalidOperationException("オブジェクト名 'orders' が無効です。");

        var editor = Editor(session);
        editor.OpenTableEditor(ShopDb, Dbo, "orders");
        await Settle(editor);

        Assert.True(editor.HasFailed);
        Assert.Contains("orders", editor.Status, StringComparison.Ordinal);
        Assert.Empty(editor.Rows);
    }

    [Fact]
    public async Task 上限まで読んだらその旨を添える()
    {
        var session = ReadWriteSession();
        session.WithEditableRows("shop", "dbo", "orders", Rows(isTruncated: true));

        var editor = await OpenAsync(session);

        Assert.Contains("先頭 100 行", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 新しい行に打ち込んで確定すると1行足す()
    {
        var session = ReadWriteSession();
        session.InsertedRow = ["3", "kudo", null, null];

        var editor = await OpenAsync(session);
        var newRow = Assert.IsType<EditableRowViewModel>(editor.NewRow);

        Fill(newRow, ordinal: 1, "kudo");
        await newRow.CommitAsync();

        // 打ち込んだ列だけを送る（触っていない列は既定値に任せる）。
        Assert.Equal("shop.dbo.orders", session.InsertedTable);
        Assert.Equal(
            [("customer", "kudo")],
            session.LastInsert!.Values.Select(value => (value.Column, value.Value)));

        // 足した行は普通の行になり、サーバーが決めた id が入る。下に新しい行がまた出る。
        Assert.False(newRow.IsNewRow);
        Assert.Equal("3", newRow.Number);
        Assert.Equal("3", newRow.Cells[0].Value);
        Assert.Equal(4, editor.Rows.Count);
        Assert.Equal("*", editor.Rows[^1].Number);
        Assert.Contains("追加", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 新しい行のセルはサーバーへ送らない()
    {
        // 行としてそろってから 1 行で足す。セルごとに UPDATE を投げるのは既存の行だけ。
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        Fill(editor.NewRow!, ordinal: 1, "kudo");

        Assert.Null(session.LastUpdate);
        Assert.Null(session.LastInsert);
        Assert.Equal("kudo", editor.NewRow!.Cells[1].Value);
    }

    [Fact]
    public async Task 何も打ち込んでいない新しい行は足さない()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        await editor.NewRow!.CommitAsync();

        Assert.Null(session.LastInsert);
        Assert.True(editor.NewRow.IsNewRow);
    }

    [Fact]
    public async Task 空欄のまま通り過ぎたセルは触っていない扱いにする()
    {
        // 空文字列を置くと、サーバーの既定値（DEFAULT 制約）が効かなくなる。
        var session = ReadWriteSession();
        session.InsertedRow = ["3", "kudo", null, null];

        var editor = await OpenAsync(session);
        var newRow = editor.NewRow!;

        // customer は打ち込み、status（nvarchar）は開いて閉じただけ。
        Fill(newRow, ordinal: 1, "kudo");
        Fill(newRow, ordinal: 2, string.Empty);

        await newRow.CommitAsync();

        Assert.Equal(["customer"], session.LastInsert!.Values.Select(value => value.Column));
    }

    [Fact]
    public async Task 新しい行を取り消すと打ちかけを捨てる()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);

        Fill(editor.NewRow!, ordinal: 1, "kudo");
        editor.CancelNewRowCommand.Execute(null);

        Assert.Null(editor.NewRow!.Cells[1].Value);
        Assert.False(editor.NewRow.HasPendingValues);
        Assert.Null(session.LastInsert);
    }

    [Fact]
    public async Task 足すのに失敗したら打ちかけを残して理由を出す()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);
        session.EditFailure = new InvalidOperationException("INSERT ステートメントが FOREIGN KEY 制約と競合しました。");

        Fill(editor.NewRow!, ordinal: 1, "kudo");
        await editor.NewRow!.CommitAsync();

        // 直してもう一度確定できるように、打ちかけはそのまま残す。
        Assert.True(editor.NewRow.IsNewRow);
        Assert.Equal("kudo", editor.NewRow.Cells[1].Value);
        Assert.True(editor.HasFailed);
        Assert.Contains("FOREIGN KEY", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 読み直せない行を足したときは読み込み直す()
    {
        // 既定値で決まる主キーなど、足した行を当てにいけないとき。画面と中身を合わせ直す。
        var session = ReadWriteSession();
        session.InsertedRow = null;

        var editor = await OpenAsync(session);
        var reads = session.EditableRowsCallCount;

        Fill(editor.NewRow!, ordinal: 1, "kudo");
        await editor.NewRow!.CommitAsync();
        await Settle(editor);

        Assert.Equal(reads + 1, session.EditableRowsCallCount);
        Assert.Contains("追加", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 行の削除は確認を取ってから消す()
    {
        var session = ReadWriteSession();
        var prompt = new FakeRowDeletionPrompt();
        var editor = await OpenAsync(session, prompt);

        await editor.Rows[0].DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, prompt.Calls);
        Assert.Equal(1, prompt.LastRowCount);
        Assert.Equal("shop.dbo.orders", session.DeletedTable);
        Assert.Equal([("id", "1")], session.LastDelete!.Criteria.Select(c => (c.Column, c.Value)));

        // 消した行はグリッドからも消え、残りの行番号は振り直す。
        Assert.Equal(2, editor.Rows.Count);
        Assert.Equal(["1", "*"], editor.Rows.Select(row => row.Number));
        Assert.Contains("削除", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 確認でやめたら消さない()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session, new FakeRowDeletionPrompt(answer: false));

        await editor.Rows[0].DeleteCommand.ExecuteAsync(null);

        Assert.Null(session.LastDelete);
        Assert.Equal(3, editor.Rows.Count);
    }

    [Fact]
    public async Task 新しい行の削除は打ちかけを捨てるだけで確認も要らない()
    {
        var session = ReadWriteSession();
        var prompt = new FakeRowDeletionPrompt();
        var editor = await OpenAsync(session, prompt);

        Fill(editor.NewRow!, ordinal: 1, "kudo");
        await editor.NewRow!.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(0, prompt.Calls);
        Assert.Null(session.LastDelete);
        Assert.False(editor.NewRow.HasPendingValues);
        Assert.Equal(3, editor.Rows.Count);
    }

    [Fact]
    public async Task 消すのに失敗したら行を残して理由を出す()
    {
        var session = ReadWriteSession();
        var editor = await OpenAsync(session);
        session.EditFailure = new InvalidOperationException("DELETE ステートメントが REFERENCE 制約と競合しました。");

        await editor.Rows[0].DeleteCommand.ExecuteAsync(null);

        Assert.Equal(3, editor.Rows.Count);
        Assert.True(editor.HasFailed);
        Assert.Contains("REFERENCE", editor.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 主キーが無いテーブルでも行は足せる()
    {
        // 足すだけならどの行かを決める必要がない（SSMS も同じ）。消すほうはできない。
        var session = ReadWriteSession();
        session.WithEditableRows(
            "shop",
            "dbo",
            "logs",
            new EditableRowSet(
                [
                    new EditableColumn(
                        "body", "xml", IsNullable: true, IsKey: false, IsReadOnly: true, IsNumeric: false,
                        IsText: false),
                    new EditableColumn(
                        "note", "nvarchar(50)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false,
                        IsText: true)
                ],
                [new string?[] { "<x/>", "書き置き" }]));

        var editor = Editor(session);
        editor.OpenTableEditor(ShopDb, Dbo, "logs");
        await Settle(editor);

        Assert.True(editor.CanInsert);
        Assert.False(editor.CanEdit);
        Assert.False(editor.CanDelete);
        Assert.NotNull(editor.NewRow);

        // 新しい行には打ち込める（既存の行は書き換えられない）。
        Assert.True(editor.NewRow!.Cells[1].IsEditable);
        Assert.False(editor.Rows[0].Cells[1].IsEditable);
    }

    [Fact]
    public async Task 読み取り専用の接続では新しい行を出さない()
    {
        var session = new FakeDatabaseSession().WithEditableRows("shop", "dbo", "orders", Rows());

        var editor = await OpenAsync(session);

        Assert.False(editor.CanInsert);
        Assert.False(editor.CanDelete);
        Assert.Null(editor.NewRow);
        Assert.Equal(2, editor.Rows.Count);
    }

    /// <summary>新しい行のセルへ打ち込んで確定する（画面での「押す → 打つ → Tab」にあたる）。</summary>
    private static void Fill(EditableRowViewModel row, int ordinal, string text)
    {
        var cell = row.Cells[ordinal];

        cell.BeginEdit();
        cell.EditText = text;
        cell.CommitAsync().GetAwaiter().GetResult();
    }

    private static Task<TableEditorViewModel> OpenAsync(FakeDatabaseSession session) =>
        OpenAsync(session, new FakeRowDeletionPrompt());

    private static async Task<TableEditorViewModel> OpenAsync(
        FakeDatabaseSession session,
        IRowDeletionPrompt prompt)
    {
        var editor = Editor(session, prompt);
        editor.OpenTableEditor(ShopDb, Dbo, "orders");
        await Settle(editor);

        return editor;
    }

    /// <summary>読み込みは右クリックの裏で始まるので、落ち着くまで待つ。</summary>
    private static async Task Settle(TableEditorViewModel editor)
    {
        while (editor.IsLoading)
        {
            await Task.Yield();
        }
    }

    private static TableEditorViewModel Editor(FakeDatabaseSession session) =>
        Editor(session, new FakeRowDeletionPrompt());

    private static TableEditorViewModel Editor(FakeDatabaseSession session, IRowDeletionPrompt prompt) =>
        new(
            session,
            new EditTableRowsUseCase(),
            new UpdateTableCellUseCase(),
            new InsertTableRowUseCase(),
            new DeleteTableRowUseCase(),
            prompt);

    private static FakeDatabaseSession ReadWriteSession()
    {
        var profile = SeedConnections.Create().First(candidate => candidate.AccessMode == AccessMode.ReadWrite);

        return new FakeDatabaseSession(profile).WithEditableRows("shop", "dbo", "orders", Rows());
    }

    /// <summary>id（IDENTITY・主キー）・customer・status・amount の 4 列。</summary>
    private static EditableRowSet Rows(bool isTruncated = false) =>
        new(
            [
                new EditableColumn(
                    "id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false),
                new EditableColumn(
                    "customer", "nvarchar(50)", IsNullable: false, IsKey: false, IsReadOnly: false, IsNumeric: false,
                    IsText: true),
                new EditableColumn(
                    "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false,
                    IsText: true),
                new EditableColumn(
                    "amount", "decimal(18, 2)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: true,
                    IsText: false)
            ],
            [
                new string?[] { "1", "tanaka", "paid", "1200.00" },
                new string?[] { "2", "sato", null, null }
            ],
            isTruncated);
}
