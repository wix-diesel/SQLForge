using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using SQLForge.Application.Query;
using SQLForge.Domain.Sql;
using SQLForge.Ui.Presentation;
using SQLForge.Ui.ViewModels;
using SQLForge.Ui.ViewModels.Workspace;

namespace SQLForge.Ui.Views;

/// <summary>
/// クエリエディタと結果ペイン。中身はビューモデルに寄せてあるので、ここでやるのは
/// AvaloniaEdit にしか置けない配線（色分けの差し込みと補完ポップアップ）だけ。
/// </summary>
public partial class QueryWorkspacePane : UserControl
{
    /// <summary>字句の種類と、Tokens.axaml の色トークンの対応。</summary>
    private static readonly (SqlTokenKind Kind, string Key)[] SyntaxBrushes =
    [
        (SqlTokenKind.Keyword, "SyntaxKeywordBrush"),
        (SqlTokenKind.Function, "SyntaxFunctionBrush"),
        (SqlTokenKind.Type, "SyntaxTypeBrush"),
        (SqlTokenKind.String, "SyntaxStringBrush"),
        (SqlTokenKind.Number, "SyntaxNumberBrush"),
        (SqlTokenKind.Comment, "SyntaxCommentBrush"),
        (SqlTokenKind.Identifier, "SyntaxIdentifierBrush"),
        (SqlTokenKind.Variable, "SyntaxIdentifierBrush"),
        (SqlTokenKind.Punctuation, "SyntaxPunctuationBrush")
    ];

    private readonly SqlColorizer _colorizer = new();

    private TextEditor? _editor;
    private CompletionWindow? _completion;

    public QueryWorkspacePane()
    {
        InitializeComponent();

        _editor = this.FindControl<TextEditor>("Editor");

        if (_editor is null)
        {
            return;
        }

        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;

        // タブを切り替えると文書が差し替わる。書きかけの位置はタブごとに覚えておき、
        // 戻ってきたらそこへ帰す（エディタは 1 つを使い回すため）。
        _editor.TextArea.Caret.PositionChanged += (_, _) => RememberCaret();
        _editor.DocumentChanged += (_, _) => RestoreCaret();

        ApplySyntaxColors();
        ActualThemeVariantChanged += (_, _) => ApplySyntaxColors();
    }

    /// <summary>今のタブの文書を編集しているときだけ、その位置を覚える。</summary>
    private void RememberCaret()
    {
        if (Current() is { } document && ReferenceEquals(_editor?.Document, document.Document))
        {
            document.CaretOffset = _editor.CaretOffset;
        }
    }

    private void RestoreCaret()
    {
        // 差し替えの途中で開いたままの候補は、もう別の文書のものなので閉じる。
        _completion?.Close();

        if (_editor?.Document is { } opened && Current() is { } document
            && ReferenceEquals(opened, document.Document))
        {
            _editor.CaretOffset = Math.Min(document.CaretOffset, opened.TextLength);
        }
    }

    /// <summary>タブ帯で中クリックしたら、そのタブを閉じる（SSMS と同じ）。</summary>
    private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton is not MouseButton.Middle)
        {
            return;
        }

        if ((e.Source as Control)?.DataContext is QueryDocumentViewModel document)
        {
            document.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private QueryDocumentViewModel? Current() =>
        (DataContext as MainWindowViewModel)?.Query.SelectedDocument;

    /// <summary>
    /// 画面へ載ってから色を引き直す。組み立てた時点では親をたどれず、
    /// テーマの色に届かないことがあるため。
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ApplySyntaxColors();
    }

    /// <summary>色分けの色をテーマから取り直す。ライトとダークの切り替えはここを通る。</summary>
    private void ApplySyntaxColors()
    {
        var brushes = new Dictionary<SqlTokenKind, IBrush>();

        foreach (var (kind, key) in SyntaxBrushes)
        {
            // テーマ別の辞書（Tokens.axaml の ThemeDictionaries）に入っているので、
            // 今のテーマを渡して引く。渡さないと変種なしの辞書しか見に行かず、何も見つからない。
            if (this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush)
            {
                brushes[kind] = brush;
            }
        }

        _colorizer.Brushes = brushes;
        _editor?.TextArea.TextView.Redraw();
    }

    /// <summary>
    /// ポップアップが開いている間に語を作らない文字を打ったら、選んでいる候補で確定する
    /// （打った文字はそのまま入る）。
    /// </summary>
    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completion is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (!IsWordCharacter(e.Text[0]))
        {
            _completion.CompletionList.RequestInsertion(e);
        }
    }

    private async void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        // 開いている間の絞り込みは AvaloniaEdit がやる。ここで開き直さない。
        if (_editor is null || _completion is not null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (!ShouldOpen(e.Text[0]) || DataContext is not MainWindowViewModel { Query.SelectedDocument: not null } viewModel)
        {
            return;
        }

        var caret = _editor.CaretOffset;
        var result = await viewModel.Query.SelectedDocument!.CompleteAsync(caret);

        // 候補を読んでいる間に打ち進められていたら、その位置の候補ではないので捨てる。
        if (result.IsEmpty || _editor.CaretOffset != caret || _completion is not null)
        {
            return;
        }

        Show(result.ReplaceOffset, caret, result.Items);
    }

    private void Show(int start, int caret, IReadOnlyList<SqlCompletionItem> items)
    {
        if (_editor is null)
        {
            return;
        }

        var window = new CompletionWindow(_editor.TextArea)
        {
            StartOffset = start,
            EndOffset = caret,
            CloseAutomatically = true
        };

        foreach (var item in items)
        {
            window.CompletionList.CompletionData.Add(new SqlCompletionData(item));
        }

        window.Closed += (_, _) => _completion = null;
        _completion = window;
        window.Show();
    }

    /// <summary>語を打ち始めたか、修飾の . を打ったところで開く。</summary>
    private static bool ShouldOpen(char typed) => typed == '.' || char.IsLetter(typed) || typed == '_';

    private static bool IsWordCharacter(char typed) =>
        char.IsLetterOrDigit(typed) || typed is '_' or '#' or '$';

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
