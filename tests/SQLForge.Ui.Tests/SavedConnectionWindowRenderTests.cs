using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Domain.Connections;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 書き出しと取り込みのダイアログが実際に組み上がって描けること。
/// XAML の記述ミスやリソース参照の取りこぼしは、ここで初めて表に出る。
/// </summary>
public class SavedConnectionWindowRenderTests
{
    [AvaloniaFact]
    public void 書き出しのダイアログが描画できる()
    {
        var dialog = new ConnectionExportDialogViewModel("prod-sales", "/home/me/prod-sales.toml");
        var window = Show(new ConnectionExportWindow { DataContext = dialog });

        Assert.Contains("接続情報の書き出し", Texts(window));
        Assert.Contains("prod-sales", Texts(window));
        Assert.Contains("参照…", ButtonTexts(window));
        Assert.Contains("書き出し", ButtonTexts(window));
    }

    [AvaloniaFact]
    public void 資格情報を含めると注意書きが出る()
    {
        var dialog = new ConnectionExportDialogViewModel("prod-sales", "/home/me/prod-sales.toml");
        var window = Show(new ConnectionExportWindow { DataContext = dialog });

        dialog.ExcludeCredentials = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsEffectivelyVisible
                && block.Text is { } text
                && text.Contains("そのまま読める形", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void 書き出し先が空なら書き出せない()
    {
        var dialog = new ConnectionExportDialogViewModel("prod-sales", string.Empty);
        var window = Show(new ConnectionExportWindow { DataContext = dialog });

        var export = window.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "書き出し");
        Assert.False(export.IsEffectivelyEnabled);

        dialog.FilePath = "/home/me/prod-sales.toml";
        Dispatcher.UIThread.RunJobs();

        Assert.True(export.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void 取り込みの確認は5択で出る()
    {
        var dialog = new ImportConflictDialogViewModel(Profile("prod-sales"));
        var window = Show(new ImportConflictWindow { DataContext = dialog });

        Assert.Contains("prod-sales は既にあります。", Texts(window));
        Assert.Contains("本番 · sqlserver · db.internal:1433", Texts(window));
        Assert.Equal(
            ["キャンセル", "すべて飛ばす", "飛ばす", "すべて置き換える", "置き換える"],
            ButtonTexts(window));
    }

    [AvaloniaFact]
    public void 取り込みの確認は選んだ答えを返す()
    {
        var dialog = new ImportConflictDialogViewModel(Profile("prod-sales"));
        Show(new ImportConflictWindow { DataContext = dialog });

        ImportConflictChoice? choice = null;
        dialog.CloseRequested += (_, value) => choice = value;

        dialog.ReplaceAllCommand.Execute(null);

        Assert.Equal(ImportConflictChoice.ReplaceAll, choice);
    }

    private static Window Show(Window window)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        return window;
    }

    private static IReadOnlyList<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

    private static IReadOnlyList<string?> ButtonTexts(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string).ToList();

    private static ConnectionProfile Profile(string name) =>
        new(ConnectionProfileId.New(),
            name,
            EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db", TlsMode.Require),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: true),
            AccessMode.ReadOnly);
}
