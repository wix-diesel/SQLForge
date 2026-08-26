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
        Assert.Equal(2, editor.Rows.Count);
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

    private static async Task<TableEditorViewModel> OpenAsync(FakeDatabaseSession session)
    {
        var editor = Editor(session);
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
        new(session, new EditTableRowsUseCase(), new UpdateTableCellUseCase());

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
