using Avalonia;

namespace SQLForge.Ui;

internal static class Program
{
    /// <summary>
    /// 起動口。UsePlatformDetect が Linux では X11、Windows では Win32、
    /// macOS では Cocoa のバックエンドを選ぶので、同じコードのまま 3 つの OS で動く。
    /// </summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia のデザイナ・ヘッドレステストがこのメソッドを名前で探すため、public のまま残す。
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
