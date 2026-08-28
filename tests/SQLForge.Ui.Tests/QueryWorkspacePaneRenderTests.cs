using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using SQLForge.Application.Catalog;
using SQLForge.Application.Editing;
using SQLForge.Application.Query;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Sql;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.Composition;
using SQLForge.Ui.Presentation;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.ViewModels.Security;
using SQLForge.Ui.ViewModels.Workspace;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// クエリエディタが実際に組み上がって描けること。
/// 色分けはテーマのリソースを引けて初めて効くので、そこまで見る。
/// </summary>
public class QueryWorkspacePaneRenderTests
{
    private static readonly DatabaseName SalesDb = new("sales_db");

    [AvaloniaFact]
    public void エディタにビューモデルの文書がつながる()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        viewModel.Query.OpenNewQuery(SalesDb);
        viewModel.Query.Sql = "SELECT 1 FROM dbo.orders";
        Dispatcher.UIThread.RunJobs();

        var editor = Editor(window);

        Assert.Same(viewModel.Query.Document, editor.Document);
        Assert.Equal("SELECT 1 FROM dbo.orders", editor.Text);
    }

    [AvaloniaFact]
    public void 色分けの色をテーマから引く()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        viewModel.Query.OpenNewQuery(SalesDb);
        viewModel.Query.Sql = "SELECT 1";
        Dispatcher.UIThread.RunJobs();

        var colorizer = Editor(window).TextArea.TextView.LineTransformers.OfType<SqlColorizer>().Single();

        // 字句の種類ぶんの色が、Tokens.axaml のトークンから引けていること。
        Assert.Contains(SqlTokenKind.Keyword, colorizer.Brushes.Keys);
        Assert.Contains(SqlTokenKind.String, colorizer.Brushes.Keys);

        Assert.True(window.TryFindResource("SyntaxKeywordBrush", window.ActualThemeVariant, out var expected));
        Assert.Same(expected, colorizer.Brushes[SqlTokenKind.Keyword]);
    }

    [AvaloniaFact]
    public void 文面を書いたエディタが描画できる()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        viewModel.Query.OpenNewQuery(SalesDb);
        viewModel.Query.Sql = "SELECT id, region -- 覚え書き\nFROM dbo.orders WHERE region = 'ゆき'";
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
    }

    [AvaloniaFact]
    public void 語を打つと補完のポップアップが出る()
    {
        var session = NewSession();
        var window = CreateWindow(out var viewModel, session, Completion(session));
        window.Show();

        viewModel.Query.OpenNewQuery(SalesDb);
        viewModel.Query.Sql = "SELECT * FROM ";
        Dispatcher.UIThread.RunJobs();

        var editor = Editor(window);
        editor.CaretOffset = viewModel.Query.Sql.Length;
        editor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        // 語を 1 文字打つ。ここから先はビューの配線（TextEntered → 候補 → ポップアップ）。
        window.KeyTextInput("o");

        // ポップアップは重ね描き（オーバーレイ）で出るので、ウィンドウの視覚ツリーに現れる。
        var list = WaitFor(() => window.GetVisualDescendants().OfType<CompletionList>().FirstOrDefault());

        Assert.Contains(list.CompletionData, item => item.Text == "dbo.orders");
    }

    /// <summary>ポップアップは候補を読み終えてから出るので、出るまで待つ。</summary>
    private static CompletionList WaitFor(Func<CompletionList?> probe)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Dispatcher.UIThread.RunJobs();

            if (probe() is { } found)
            {
                return found;
            }

            Thread.Sleep(10);
        }

        throw new InvalidOperationException("補完のポップアップが出ませんでした。");
    }

    private static SqlCompletionUseCase Completion(FakeDatabaseSession session) =>
        new(new SchemaCache(session, new ListSchemasUseCase(), new ListTablesUseCase(), new ListColumnsUseCase()));

    private static FakeDatabaseSession NewSession() =>
        new FakeDatabaseSession()
            .WithSchemas("sales_db", new SchemaDescriptor(new SchemaName("dbo")))
            .WithTables("sales_db", "dbo", new TableDescriptor(new SchemaName("dbo"), "orders"));

    private static TextEditor Editor(Window window) =>
        window.GetVisualDescendants().OfType<TextEditor>().Single();

    private static MainWindow CreateWindow(out MainWindowViewModel viewModel) =>
        CreateWindow(out viewModel, new FakeDatabaseSession(), completion: null);

    private static MainWindow CreateWindow(
        out MainWindowViewModel viewModel,
        FakeDatabaseSession session,
        SqlCompletionUseCase? completion)
    {
        viewModel = NewViewModel(session, completion);
        var window = new MainWindow { DataContext = viewModel, Width = 1200, Height = 760 };

        window.ApplyPlatform(PlatformProfiles.ForCurrentHost());

        return window;
    }

    private static MainWindowViewModel NewViewModel(FakeDatabaseSession session, SqlCompletionUseCase? completion)
    {
        var query = new QueryEditorViewModel(session, new ExecuteQueryUseCase(), completion);
        var tableEditor = new TableEditorViewModel(
            session,
            new EditTableRowsUseCase(),
            new UpdateTableCellUseCase(),
            new InsertTableRowUseCase(),
            new DeleteTableRowUseCase(),
            new FakeRowDeletionPrompt());

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
}
