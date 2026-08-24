using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using SQLForge.Ui.Composition;
using SQLForge.Ui.Presentation;
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

    [AvaloniaFact]
    public void 一覧の行を押すとその接続を開こうとする()
    {
        // 左ペインの行から自動接続までが XAML ごしに繋がっていること。
        var window = CreateWindow(out var viewModel);
        window.Show();
        WaitFor(() => viewModel.SavedConnections.Entries.Count > 0);

        Click(window, RowOf(window, "staging-eu"));

        // 見本データにはパスワードを預けていないので、接続の前に入力を促す。
        WaitFor(() => viewModel.HasStatus);
        Assert.Equal("staging-eu", viewModel.Form.Name);
        Assert.Contains("パスワード", viewModel.StatusHeadline, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void OS統合認証を選ぶと利用者名の欄がOSアカウントの表示に入れ替わる()
    {
        // 認証方式ごとの出し分けが XAML ごしに効いていること
        // （伏せ忘れると、使われない利用者名を打てる欄が残ってしまう）。
        var window = CreateWindow(out var viewModel);
        window.Show();
        WaitFor(() => viewModel.SavedConnections.SelectedItem is not null);

        viewModel.Form.Authentication = AuthenticationChoice.For(AuthenticationMethod.Integrated);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == viewModel.Form.IntegratedAccountName && text.IsEffectivelyVisible);

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
    }

    /// <summary>指定した名前の接続行（の中の名前を出している要素）を探す。</summary>
    private static Visual RowOf(Window window, string name) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(text => text.DataContext is SavedConnectionItemViewModel item && item.Name == name);

    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("行の位置を求められません。");

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static ConnectWindow CreateWindow(out ConnectDialogViewModel viewModel)
    {
        // 実際の合成に、保存済み接続とキーリングだけ見本データのものを差し替えて組む
        // （利用者のホームにある connections.toml と OS のキーリングを触らないため）。
        var services = new ServiceCollection();
        AppServices.Configure(services);
        services.AddSingleton<IConnectionProfileRepository, InMemoryConnectionProfileRepository>();
        services.AddSingleton<ISecretStore, InMemorySecretStore>();

        return CreateWindow(services.BuildServiceProvider(), out viewModel);
    }

    private static ConnectWindow CreateWindow(IServiceProvider services, out ConnectDialogViewModel viewModel)
    {
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
