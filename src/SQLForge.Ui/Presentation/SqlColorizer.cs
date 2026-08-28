using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SQLForge.Domain.Sql;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// クエリエディタの色分け。<see cref="SqlLexer"/> が切った字句へ、
/// Themes/Tokens.axaml の Syntax*Brush を当てるだけの表示用の処理。
///
/// 文法は TextMate ではなく自前の字句解析器から取る。補完と整形が同じ字句を見ること、
/// 色をテーマのトークンから引けること（ライト・ダークがそのまま切り替わること）の 2 つが
/// 理由で、TextMate の文法とテーマは入れていない。
/// </summary>
public sealed class SqlColorizer : DocumentColorizingTransformer
{
    private IReadOnlyDictionary<SqlTokenKind, IBrush> _brushes =
        new Dictionary<SqlTokenKind, IBrush>();

    private ITextSourceVersion? _version;
    private IReadOnlyList<SqlToken> _tokens = [];

    /// <summary>字句の種類ごとの色。テーマが変わったら差し替える。</summary>
    public IReadOnlyDictionary<SqlTokenKind, IBrush> Brushes
    {
        get => _brushes;
        set => _brushes = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var document = CurrentContext.Document;
        var tokens = TokensOf(document);

        for (var index = FirstTokenAt(tokens, line.Offset); index < tokens.Count; index++)
        {
            var token = tokens[index];

            if (token.Offset >= line.EndOffset)
            {
                break;
            }

            Paint(token, line);
        }
    }

    private void Paint(SqlToken token, DocumentLine line)
    {
        if (!_brushes.TryGetValue(token.Kind, out var brush))
        {
            return;
        }

        // 複数行にまたがるコメントや文字列は、この行に重なっている分だけを塗る。
        var start = Math.Max(token.Offset, line.Offset);
        var end = Math.Min(token.End, line.EndOffset);

        if (start < end)
        {
            ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    /// <summary>
    /// 文面ぜんぶを切り直すのは版が変わったときだけ。1 回の再描画で行ごとに
    /// 切り直すと、行数の 2 乗に効いてしまう。
    /// </summary>
    private IReadOnlyList<SqlToken> TokensOf(TextDocument document)
    {
        var version = document.Version;

        if (version is not null
            && _version is not null
            && version.BelongsToSameDocumentAs(_version)
            && version.CompareAge(_version) == 0)
        {
            return _tokens;
        }

        _tokens = SqlLexer.Tokenize(document.Text);
        _version = version;

        return _tokens;
    }

    /// <summary>この位置に重なる最初の字句を二分探索で見つける。</summary>
    private static int FirstTokenAt(IReadOnlyList<SqlToken> tokens, int offset)
    {
        var low = 0;
        var high = tokens.Count;

        while (low < high)
        {
            var middle = (low + high) / 2;

            if (tokens[middle].End <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
