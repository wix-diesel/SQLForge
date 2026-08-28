using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
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

    /// <summary>今エディタに出している文書。差し替えの途中で位置を書き戻さないよう、ビュー側で持つ。</summary>
    private QueryDocumentViewModel? _shown;

    /// <summary>文書の差し替え中。この間のキャレットの動きは打ち手のものではない。</summary>
    private bool _switching;

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
        _editor.DocumentChanged += (_, _) => OnDocumentChanged();

        ApplySyntaxColors();
        ActualThemeVariantChanged += (_, _) => ApplySyntaxColors();
    }

    /// <summary>
    /// 打ち手が動かしたキャレットだけを覚える。
    ///
    /// 差し替えの前後で AvaloniaEdit 自身もキャレットを 0 へ戻すので、素直に受けると
    /// 覚えておいた位置をその 0 で塗り潰してしまう。出している文書はこちらで持ち（<see cref="_shown"/>）、
    /// 差し替えの間（<see cref="_switching"/>）は覚えないことで、その取りこぼしを防ぐ。
    /// </summary>
    private void RememberCaret()
    {
        if (_switching || _editor is null || _shown is not { } document
            || !ReferenceEquals(_editor.Document, document.Document))
        {
            return;
        }

        document.CaretOffset = _editor.CaretOffset;
    }

    /// <summary>
    /// 文書が差し替わった。AvaloniaEdit 側の後始末が済んでから位置を入れ直したいので、
    /// 戻すのは次の待ち行列へ回す。
    /// </summary>
    private void OnDocumentChanged()
    {
        // 開いたままの候補は、もう別の文書のものなので閉じる。
        _completion?.Close();
        _shown = null;
        _switching = true;

        Dispatcher.UIThread.Post(RestoreCaret, DispatcherPriority.Background);
    }

    private void RestoreCaret()
    {
        _switching = false;

        if (_editor?.Document is not { } opened || Current() is not { } document
            || !ReferenceEquals(opened, document.Document))
        {
            return;
        }

        _shown = document;
        _editor.CaretOffset = Math.Min(document.CaretOffset, opened.TextLength);
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

        if (!ShouldOpen(e.Text[0]) || Current() is not { } document)
        {
            return;
        }

        var caret = _editor.CaretOffset;
        var result = await document.CompleteAsync(caret);

        // 候補を読んでいる間に打ち進められたか、別のタブへ移っていたら、
        // もうその位置・その実行先の候補ではないので捨てる。
        if (result.IsEmpty
            || _completion is not null
            || !ReferenceEquals(Current(), document)
            || !ReferenceEquals(_editor.Document, document.Document)
            || _editor.CaretOffset != caret)
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
