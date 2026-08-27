using Avalonia.Platform.Storage;

namespace SQLForge.Ui.Views;

/// <summary>書き出し・取り込みのファイル ダイアログで出す種別。中身は TOML。</summary>
internal static class ConnectionArchiveFile
{
    public static FilePickerFileType Type { get; } = new("SQLForge の接続情報")
    {
        Patterns = ["*.toml"],
        MimeTypes = ["text/plain"]
    };
}
