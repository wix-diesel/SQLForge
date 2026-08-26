using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Application.Catalog;
using SQLForge.Application.Editing;
using SQLForge.Application.Query;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Domain.Editing;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.Composition;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.ViewModels.Workspace;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 編集グリッドが実際に組み上がって描けること。
/// セルのテンプレートは「表示」と「入力欄」の 2 枚が重なっているので、切り替わることまで見る。
/// </summary>
public class TableEditorPaneRenderTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");
    private static readonly SchemaName Dbo = new("dbo");

    [AvaloniaFact]
    public void 編集グリッドに列と値が並ぶ()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        var pane = window.GetVisualDescendants().OfType<TableEditorPane>().Single();
        Assert.False(pane.IsVisible);

        viewModel.TableEditor.OpenTableEditor(SalesDb, Dbo, "orders");
        WaitFor(() => viewModel.TableEditor.Rows.Count > 0);

        Assert.True(pane.IsVisible);

        // 見出しとセルは別のテンプレートなので、両方が出ることを確かめる。
        WaitFor(() => Texts(window).Contains("paid"));
        Assert.Contains("status", Texts(window));

        // NULL は「NULL」という値の入ったセルと見分けが付くように出す。
        Assert.Contains("NULL", Texts(window));
    }

    [AvaloniaFact]
    public void セルを開くと入力欄が重なって出る()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        viewModel.TableEditor.OpenTableEditor(SalesDb, Dbo, "orders");
        WaitFor(() => viewModel.TableEditor.Rows.Count > 0);

        var cell = viewModel.TableEditor.Rows[0].Cells[1];
        cell.BeginEdit();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(box => ReferenceEquals(box.DataContext, cell));

        Assert.True(editor.IsVisible);
        Assert.Equal("paid", editor.Text);
    }

    [AvaloniaFact]
    public void 編集グリッドを開くとクエリエディタは引っ込む()
    {
        // 作業領域は 1 つしかないので、開いたほうを前に出す。閉じれば元が戻る。
        var window = CreateWindow(out var viewModel);
        window.Show();

        viewModel.Query.OpenNewQuery(SalesDb);
        Dispatcher.UIThread.RunJobs();

        var query = window.GetVisualDescendants().OfType<QueryWorkspacePane>().Single();
        Assert.True(query.IsVisible);

        viewModel.TableEditor.OpenTableEditor(SalesDb, Dbo, "orders");
        WaitFor(() => viewModel.TableEditor.Rows.Count > 0);

        Assert.False(query.IsVisible);

        viewModel.TableEditor.CloseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(query.IsVisible);
    }

    private static MainWindow CreateWindow(out MainWindowViewModel viewModel)
    {
        viewModel = NewViewModel(NewSession());
        var window = new MainWindow { DataContext = viewModel };

        window.ApplyPlatform(PlatformProfiles.ForCurrentHost());

        return window;
    }

    private static MainWindowViewModel NewViewModel(FakeDatabaseSession session)
    {
        var query = new QueryEditorViewModel(session, new ExecuteQueryUseCase());
        var tableEditor = new TableEditorViewModel(session, new EditTableRowsUseCase(), new UpdateTableCellUseCase());

        return new MainWindowViewModel(
            session,
            PlatformProfiles.ForCurrentHost(),
            new CatalogContext(
                session,
                new ListDatabasesUseCase(),
                new ListSchemasUseCase(),
                new ListTablesUseCase(),
                new ListColumnsUseCase(),
                new ListStoredProceduresUseCase(),
                new ListStoredProcedureParametersUseCase(),
                query)
            {
                TableEditor = tableEditor,
                Security = new DatabaseSecurityContext(new ListDatabaseUsersUseCase())
            },
            query,
            tableEditor);
    }

    /// <summary>書き込みできる接続で開く。読み取り専用だとセルが開かない。</summary>
    private static FakeDatabaseSession NewSession()
    {
        var profile = SeedConnections.Create().First(candidate => candidate.AccessMode == AccessMode.ReadWrite);

        return new FakeDatabaseSession(profile).WithEditableRows(
            "sales_db",
            "dbo",
            "orders",
            new EditableRowSet(
                [
                    new EditableColumn(
                        "id", "int", IsNullable: false, IsKey: true, IsReadOnly: true, IsNumeric: true, IsText: false),
                    new EditableColumn(
                        "status", "nvarchar(20)", IsNullable: true, IsKey: false, IsReadOnly: false, IsNumeric: false,
                        IsText: true)
                ],
                [
                    new string?[] { "1", "paid" },
                    new string?[] { "2", null }
                ]));
    }

    private static IReadOnlyList<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

    private static void WaitFor(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "期待した状態になりませんでした。");
    }
}
