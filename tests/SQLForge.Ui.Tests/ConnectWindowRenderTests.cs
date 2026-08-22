using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SQLForge.Ui.Composition;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.Views;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 起動時の画面が実際に組み上がって描けることを確認する。
/// XAML の記述ミスやリソース参照の取りこぼしは、ここで初めて表に出る。
/// </summary>
public class ConnectWindowRenderTests
{
    [AvaloniaFact]
    public void 接続ダイアログが描画できる()
    {
        var window = CreateWindow(out _);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(880, (int)window.Bounds.Width);
        Assert.Equal(640, (int)window.Bounds.Height);
    }

    [AvaloniaFact]
    public void 保存済み接続が環境タグごとに並ぶ()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        WaitFor(() => viewModel.SavedConnections.Entries.Count > 0);

        var headers = viewModel.SavedConnections.Entries.OfType<ConnectionGroupHeaderViewModel>().ToList();
        var items = viewModel.SavedConnections.Entries.OfType<SavedConnectionItemViewModel>().ToList();

        Assert.Equal(["本番", "ステージング", "ローカル"], headers.Select(header => header.Title));
        Assert.Equal(5, items.Count);
    }

    [AvaloniaFact]
    public void 一覧の先頭が選ばれ入力欄に写る()
    {
        var window = CreateWindow(out var viewModel);
        window.Show();

        WaitFor(() => viewModel.SavedConnections.SelectedItem is not null);

        Assert.Equal("prod-analytics", viewModel.Form.Name);
        Assert.Equal("10.2.0.14", viewModel.Form.Host);
        Assert.Equal("5432", viewModel.Form.Port);
        Assert.True(viewModel.Form.IsReadOnly, "本番タグの接続は既定で読み取り専用になる。");
    }

    private static ConnectWindow CreateWindow(out ConnectDialogViewModel viewModel)
    {
        var services = AppServices.Build();
        viewModel = services.GetRequiredService<ConnectDialogViewModel>();
        _ = viewModel.InitializeAsync();

        return new ConnectWindow { DataContext = viewModel };
    }

    private static void WaitFor(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "期待した状態になりませんでした。");
    }
}
