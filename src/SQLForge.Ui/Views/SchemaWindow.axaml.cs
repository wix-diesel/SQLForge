using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// スキーマのプロパティ ダイアログ。ページは 1 枚だけなので、
/// ほかのセキュリティ ダイアログと違ってページの選択を持たない。
/// </summary>
public partial class SchemaWindow : Window
{
    public SchemaWindow()
    {
        InitializeComponent();

        // 開いた直後は名前欄から入力できるようにする。
        Opened += (_, _) => this.FindControl<TextBox>("NameBox")?.Focus();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
