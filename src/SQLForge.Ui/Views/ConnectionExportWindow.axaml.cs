using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SQLForge.Ui.ViewModels;

namespace SQLForge.Ui.Views;

/// <summary>
/// 接続情報の書き出しダイアログ。置き場所を選ぶファイル ダイアログは
/// OS のものを出すので、ここ（ビュー）で開く。
/// </summary>
public partial class ConnectionExportWindow : Window
{
    public ConnectionExportWindow()
    {
        InitializeComponent();

        Opened += (_, _) => this.FindControl<TextBox>("PathBox")?.Focus();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionExportDialogViewModel dialog || GetTopLevel(this) is not { } top)
        {
            return;
        }

        try
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = dialog.Title,
                SuggestedFileName = Path.GetFileName(dialog.FilePath),
                DefaultExtension = "toml",
                FileTypeChoices = [ConnectionArchiveFile.Type]
            }).ConfigureAwait(true);

            if (file is not null)
            {
                dialog.FilePath = file.Path.LocalPath;
            }
        }
        catch (Exception)
        {
            // ファイル ダイアログを出せない環境（ポータルの無い Linux など）では、
            // 入力欄に直接打ってもらう。閉じてしまわないよう、ここで止める。
        }
    }
}
