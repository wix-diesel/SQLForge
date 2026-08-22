using Avalonia;
using Avalonia.Headless;
using SQLForge.Ui;
using SQLForge.Ui.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace SQLForge.Ui.Tests;

/// <summary>
/// ヘッドレスでアプリを起動するための設定。実際に描画させたいので
/// UseHeadlessDrawing を false にして Skia で描かせる。
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
