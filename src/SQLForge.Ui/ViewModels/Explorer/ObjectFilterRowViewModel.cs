using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Domain.Filtering;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「フィルターの設定」の 1 行。SSMS と同じく、プロパティ・演算子・値の 3 つ組で並び、
/// 行そのものは増減しない（値を空にした行が「条件なし」を表す）。
/// </summary>
public sealed partial class ObjectFilterRowViewModel : ObservableObject
{
    [ObservableProperty] private FilterOperatorChoiceViewModel _operator;

    [ObservableProperty] private string _value = string.Empty;

    [ObservableProperty] private string _bound = string.Empty;

    public ObjectFilterRowViewModel(ObjectFilterProperty property)
    {
        Property = property;
        Operators = property.IsDate()
            ? FilterOperatorChoiceViewModel.ForDate
            : FilterOperatorChoiceViewModel.ForText;
        _operator = Operators[0];
    }

    public ObjectFilterProperty Property { get; }

    public string DisplayName => Property.DisplayName();

    /// <summary>ダイアログの下に出す説明。</summary>
    public string Description => Property.Description();

    /// <summary>日付の行か。入力欄の書き方と演算子の並びが変わる。</summary>
    public bool IsDate => Property.IsDate();

    public IReadOnlyList<FilterOperatorChoiceViewModel> Operators { get; }

    /// <summary>「次の間」を選んだときだけ、終わりの日の入力欄を出す。</summary>
    public bool ShowBound => Operator.NeedsBound;

    /// <summary>値が入っているか。空の行は条件にならない。</summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(Value);

    public string ValueHint => IsDate ? "yyyy/MM/dd" : "値";

    /// <summary>入力を空へ戻す。「フィルターのクリア」から呼ばれる。</summary>
    public void Clear()
    {
        Operator = Operators[0];
        Value = string.Empty;
        Bound = string.Empty;
    }

    /// <summary>今かかっている条件を入力欄へ写す。開いたときに前回の設定が出るようにする。</summary>
    public void Restore(TextFilterClause clause)
    {
        ArgumentNullException.ThrowIfNull(clause);

        Operator = FilterOperatorChoiceViewModel.Of(clause.Operator);
        Value = clause.Value;
    }

    /// <summary>今かかっている条件を入力欄へ写す（日付の行）。</summary>
    public void Restore(DateFilterClause clause)
    {
        ArgumentNullException.ThrowIfNull(clause);

        Operator = FilterOperatorChoiceViewModel.Of(clause.Operator);
        Value = ObjectFilterDates.Format(clause.Value);
        Bound = clause.Bound is { } bound ? ObjectFilterDates.Format(bound) : string.Empty;
    }

    partial void OnOperatorChanged(FilterOperatorChoiceViewModel value) => OnPropertyChanged(nameof(ShowBound));
}
