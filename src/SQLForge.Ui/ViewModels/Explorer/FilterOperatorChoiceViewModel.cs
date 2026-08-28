using SQLForge.Domain.Filtering;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「フィルターの設定」で選ぶ演算子 1 つ。文字列の行と日付の行では並ぶものが違うので、
/// どちらか一方だけを持つ器にしてある（ドロップダウンは 1 つの型しか並べられないため）。
/// </summary>
public sealed record FilterOperatorChoiceViewModel
{
    private FilterOperatorChoiceViewModel(TextFilterOperator? text, DateFilterOperator? date)
    {
        Text = text;
        Date = date;
    }

    /// <summary>文字列の演算子。日付の選択肢では null。</summary>
    public TextFilterOperator? Text { get; }

    /// <summary>日付の演算子。文字列の選択肢では null。</summary>
    public DateFilterOperator? Date { get; }

    public string DisplayName => Text?.DisplayName() ?? Date?.DisplayName() ?? string.Empty;

    /// <summary>終わりの日（2 つめの入力欄）が要る演算子か。</summary>
    public bool NeedsBound => Date is { } date && date.NeedsBound();

    /// <summary>文字列のプロパティに並べる選択肢。</summary>
    public static IReadOnlyList<FilterOperatorChoiceViewModel> ForText { get; } =
        TextFilterOperators.All.Select(@operator => new FilterOperatorChoiceViewModel(@operator, null)).ToList();

    /// <summary>日付のプロパティに並べる選択肢。</summary>
    public static IReadOnlyList<FilterOperatorChoiceViewModel> ForDate { get; } =
        DateFilterOperators.All.Select(@operator => new FilterOperatorChoiceViewModel(null, @operator)).ToList();

    public static FilterOperatorChoiceViewModel Of(TextFilterOperator @operator) =>
        ForText.First(choice => choice.Text == @operator);

    public static FilterOperatorChoiceViewModel Of(DateFilterOperator @operator) =>
        ForDate.First(choice => choice.Date == @operator);
}
