using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLForge.Ui.Views;

/// <summary>
/// クエリエディタと結果ペイン。中身はすべてビューモデルに寄せてあるので、
/// ここでやるのは既定スタイルの打ち消しだけ。
/// </summary>
public partial class QueryWorkspacePane : UserControl
{
    public QueryWorkspacePane()
    {
        InitializeComponent();

        // TextBox の既定スタイルは 1 行ぶんの高さを与える。エディタは器いっぱいに
        // 広がってほしいので、ここで「指定なし」へ戻す（ローカル値はスタイルより強い）。
        if (this.FindControl<TextBox>("Editor") is { } editor)
        {
            editor.Height = double.NaN;
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
