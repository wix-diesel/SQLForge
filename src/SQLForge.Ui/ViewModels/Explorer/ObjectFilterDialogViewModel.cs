using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Filtering;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// SSMS の「フィルターの設定」にあたるダイアログ。条件にできるプロパティを 1 行ずつ並べ、
/// 演算子と値を入れてもらう。行は増えも減りもせず、値が空の行は条件にならない。
///
/// 条件どうしは AND で重なる（SSMS と同じ）。
/// </summary>
public sealed partial class ObjectFilterDialogViewModel : ObservableObject
{
    [ObservableProperty] private ObjectFilterRowViewModel? _selectedRow;

    [ObservableProperty] private string? _errorMessage;

    public ObjectFilterDialogViewModel(
        string path,
        IReadOnlyList<ObjectFilterProperty> properties,
        ObjectFilter current)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(current);

        FolderPath = path;
        Rows = properties.Select(property => new ObjectFilterRowViewModel(property)).ToList();
        _selectedRow = Rows.FirstOrDefault();

        Restore(current);
    }

    /// <summary>どの見出しの設定か（例: sales_db/dbo/テーブル）。</summary>
    public string FolderPath { get; }

    public IReadOnlyList<ObjectFilterRowViewModel> Rows { get; }

    /// <summary>OK で組み上がった絞り込み。キャンセルなら null のまま。</summary>
    public ObjectFilter? Result { get; private set; }

    public bool HasError => ErrorMessage is { Length: > 0 };

    /// <summary>閉じてよくなった合図。true なら OK。</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>「フィルターのクリア」。入力を空へ戻すだけで、閉じるのは OK を押してから。</summary>
    [RelayCommand]
    private void Clear()
    {
        foreach (var row in Rows)
        {
            row.Clear();
        }

        ErrorMessage = null;
    }

    /// <summary>OK。読めない値があれば理由を出して閉じない。</summary>
    [RelayCommand]
    private void Accept()
    {
        if (!TryBuild(out var filter, out var error))
        {
            ErrorMessage = error;
            return;
        }

        Result = filter;
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    /// <summary>入力から絞り込みを組む。値が空の行は読み飛ばす。</summary>
    private bool TryBuild(out ObjectFilter filter, out string? error)
    {
        filter = ObjectFilter.None;
        error = null;

        var texts = new List<TextFilterClause>();
        DateFilterClause? createdAt = null;

        foreach (var row in Rows.Where(row => row.HasValue))
        {
            if (!row.IsDate)
            {
                texts.Add(new TextFilterClause(row.Property, row.Operator.Text!.Value, row.Value.Trim()));
                continue;
            }

            if (!ObjectFilterDates.TryParse(row.Value, out var value))
            {
                error = $"{row.DisplayName}は {ObjectFilterDates.Pattern} の形で入力してください。";
                return false;
            }

            DateOnly? bound = null;

            if (row.Operator.NeedsBound)
            {
                if (!ObjectFilterDates.TryParse(row.Bound, out var parsed))
                {
                    error = $"「{row.Operator.DisplayName}」には終わりの日も"
                        + $" {ObjectFilterDates.Pattern} の形で入力してください。";
                    return false;
                }

                bound = parsed;
            }

            createdAt = new DateFilterClause(row.Operator.Date!.Value, value, bound);
        }

        filter = new ObjectFilter(texts, createdAt);
        return true;
    }

    private void Restore(ObjectFilter current)
    {
        foreach (var clause in current.Texts)
        {
            Rows.FirstOrDefault(row => row.Property == clause.Property && !row.IsDate)?.Restore(clause);
        }

        if (current.CreatedAt is { } createdAt)
        {
            Rows.FirstOrDefault(row => row.IsDate)?.Restore(createdAt);
        }
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
}
