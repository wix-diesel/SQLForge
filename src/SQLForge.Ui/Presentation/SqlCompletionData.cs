using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using SQLForge.Application.Query;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// 補完のポップアップ 1 行。ユースケースが作った候補を AvaloniaEdit へ渡すための包み。
/// </summary>
public sealed class SqlCompletionData(SqlCompletionItem item) : ICompletionData
{
    private readonly SqlCompletionItem _item = item ?? throw new ArgumentNullException(nameof(item));

    /// <summary>アイコンはまだ持たない（種類は右の補足で見分ける）。</summary>
    public IImage Image => null!;

    /// <summary>絞り込みに使う文字列。打った文字と突き合わせるのはこちら。</summary>
    public string Text => _item.Label;

    /// <summary>一覧に出す中身。</summary>
    public object Content => _item.Label;

    /// <summary>右に薄く出す補足。列なら「テーブル名 · 型」。</summary>
    public object Description => _item.Detail ?? string.Empty;

    /// <summary>並びはユースケースが決めた順を保つので、優先度は付けない。</summary>
    public double Priority => 0;

    /// <summary>打ちかけの語を候補で置き換える。要るときだけ引用符が付く。</summary>
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        ArgumentNullException.ThrowIfNull(completionSegment);

        textArea.Document.Replace(completionSegment, _item.InsertText);
    }
}
