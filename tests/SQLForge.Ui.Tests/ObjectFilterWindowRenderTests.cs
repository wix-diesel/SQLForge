using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLForge.Domain.Filtering;
using SQLForge.Ui.ViewModels.Explorer;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 「フィルターの設定」ダイアログが実際に組み上がって描けること。
/// XAML の記述ミスやリソース参照の取りこぼしは、ここで初めて表に出る。
/// </summary>
public class ObjectFilterWindowRenderTests
{
    [AvaloniaFact]
    public void フィルターの設定が描画できる()
    {
        var window = Show(NewDialog());
        var texts = Texts(window);

        Assert.Contains("フィルターの設定", texts);
        Assert.Contains("sales_db/dbo/テーブル", texts);

        // 条件にできるプロパティが 1 行ずつ並ぶ。
        Assert.Contains("名前", texts);
        Assert.Contains("作成日", texts);

        // 下の説明欄には、選んでいる行（開いた直後は 1 行目）の説明が出る。
        Assert.Contains(texts, text => text is not null && text.StartsWith("名前で絞り込みます", StringComparison.Ordinal));

        Assert.Contains("OK", ButtonTexts(window));
        Assert.Contains("フィルターのクリア", ButtonTexts(window));
    }

    [AvaloniaFact]
    public void 次の間を選ぶと終わりの日の欄が出る()
    {
        var dialog = NewDialog();
        var window = Show(dialog);

        var created = dialog.Rows[1];
        Assert.True(created.IsDate);
        Assert.False(created.ShowBound);

        // 「次の間」でないうちは、値の入力欄は行ごとに 1 つずつ。
        Assert.Equal(2, VisibleBoxes(window));

        created.Operator = FilterOperatorChoiceViewModel.Of(DateFilterOperator.Between);
        Dispatcher.UIThread.RunJobs();

        Assert.True(created.ShowBound);
        Assert.Equal(3, VisibleBoxes(window));
    }

    /// <summary>今そこに出ている入力欄の数。「次の間」で 2 つめの日付が増えるのを見る。</summary>
    private static int VisibleBoxes(Window window) =>
        window.GetVisualDescendants().OfType<TextBox>().Count(box => box.IsVisible);

    private static ObjectFilterDialogViewModel NewDialog() =>
        new(
            "sales_db/dbo/テーブル",
            [ObjectFilterProperty.Name, ObjectFilterProperty.CreatedAt],
            ObjectFilter.None);

    private static Window Show(ObjectFilterDialogViewModel dialog)
    {
        var window = new ObjectFilterWindow { DataContext = dialog };
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
}
