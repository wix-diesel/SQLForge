using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Editing;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Editing;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// テーブルの先頭 N 行を直に編集するグリッド。SSMS の「上位 200 行の編集」にあたる。
///
/// 保存ボタンは持たない。セルを確定するたびにその 1 セルだけを書き戻し、
/// 通ったかどうかをその場で見せる（SSMS の編集グリッドと同じ）。
/// </summary>
public sealed partial class TableEditorViewModel : ObservableObject, ITableEditorLauncher
{
    private readonly IDatabaseSession _session;
    private readonly EditTableRowsUseCase _editRows;
    private readonly UpdateTableCellUseCase _updateCell;

    private DatabaseName? _database;
    private SchemaName? _schema;
    private string? _table;

    /// <summary>読み込んだときの列の素性。書き戻すときの条件を組むのに使う。</summary>
    private IReadOnlyList<EditableColumn> _definitions = [];

    /// <summary>読み直した回数。待っている間に読み直されたかを見分けるのに使う。</summary>
    private int _generation;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _targetDatabase = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _hasFailed;

    /// <summary>グリッドで値を書き換えられるか。</summary>
    [ObservableProperty] private bool _canEdit;

    /// <summary>書き換えられないときの理由。読めるだけの状態で開いたことを画面に出す。</summary>
    [ObservableProperty] private string? _readOnlyReason;

    [ObservableProperty] private IReadOnlyList<EditableColumnViewModel> _columns = [];

    public TableEditorViewModel(
        IDatabaseSession session,
        EditTableRowsUseCase editRows,
        UpdateTableCellUseCase updateCell)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _editRows = editRows ?? throw new ArgumentNullException(nameof(editRows));
        _updateCell = updateCell ?? throw new ArgumentNullException(nameof(updateCell));
    }

    public ObservableCollection<EditableRowViewModel> Rows { get; } = [];

    public string MaxRowsLabel =>
        $"先頭 {EditTableRowsUseCase.DefaultMaxRows.ToString("N0", CultureInfo.InvariantCulture)} 行";

    /// <summary>読み取り専用で開いている接続。グリッドからの書き換えも止める。</summary>
    public bool IsReadOnlyConnection => _session.Profile.IsReadOnly;

    public void OpenTableEditor(DatabaseName database, SchemaName schema, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new ArgumentException("テーブル名は空にできません。", nameof(table));
        }

        _database = database;
        _schema = schema;
        _table = table;

        TargetDatabase = database.Value;
        Title = $"{schema.Value}.{table}";
        IsOpen = true;

        // 右クリックは UI スレッドの操作なので、読み込みは投げっぱなしにする
        // （LoadAsync が例外を受け止める）。
        _ = LoadAsync(CancellationToken.None);
    }

    /// <summary>サーバーから読み直す。ほかで書き換えられた行を取り込むのにも使う。</summary>
    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    /// <summary>グリッドを畳む。</summary>
    [RelayCommand]
    private void Close() => IsOpen = false;

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_database is not { } database || _schema is not { } schema || _table is not { } table)
        {
            return;
        }

        var generation = ++_generation;

        IsLoading = true;
        HasFailed = false;
        Status = string.Empty;
        Rows.Clear();

        try
        {
            var rows = await _editRows
                .ExecuteAsync(_session, database, schema, table, EditTableRowsUseCase.DefaultMaxRows, cancellationToken)
                .ConfigureAwait(true);

            if (IsCurrent(generation))
            {
                Show(rows);
            }
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた・別の読み込みに追い越された。何もしない。
        }
        catch (Exception exception)
        {
            // 権限不足やテーブルの消失はここへ来る。理由はサーバーの言葉のまま出す。
            if (IsCurrent(generation))
            {
                ShowFailure(exception.Message);
            }
        }
        finally
        {
            if (IsCurrent(generation))
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// セル 1 つを書き戻す。<see cref="EditableCellViewModel"/> から呼ばれる。
    ///
    /// 通ったときだけ表示を新しい値にする。落ちたときは元の値のままにして、
    /// 理由を下の一行に出す（画面とサーバーが食い違ったままになるのを避ける）。
    /// </summary>
    internal async Task CommitAsync(EditableCellViewModel cell, string? value)
    {
        ArgumentNullException.ThrowIfNull(cell);

        if (_database is not { } database || _schema is not { } schema || _table is not { } table)
        {
            return;
        }

        var generation = _generation;

        var request = new TableCellEditRequest(
            database, schema, table, _definitions, cell.Row.Values, cell.Ordinal, value);

        try
        {
            await _updateCell.ExecuteAsync(_session, request).ConfigureAwait(true);

            if (!IsCurrent(generation))
            {
                return;
            }

            cell.Accept(value);
            Status = $"{cell.Column.Name} を更新しました。";
            HasFailed = false;
        }
        catch (Exception exception)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            cell.Reject();
            ShowFailure(exception.Message);
        }
    }

    /// <summary>読み込んだ行をグリッドへ組み直す。</summary>
    private void Show(EditableRowSet rows)
    {
        _definitions = rows.Columns;
        CanEdit = rows.CanEdit && !IsReadOnlyConnection;
        ReadOnlyReason = DescribeReadOnly(rows);
        Columns = BuildColumns(rows);

        Rows.Clear();

        for (var index = 0; index < rows.Rows.Count; index++)
        {
            Rows.Add(new EditableRowViewModel(this, index + 1, Columns, rows.Rows[index]));
        }

        Status = DescribeStatus(rows);
        HasFailed = false;
    }

    private void ShowFailure(string message)
    {
        Status = message;
        HasFailed = true;
    }

    private IReadOnlyList<EditableColumnViewModel> BuildColumns(EditableRowSet rows)
    {
        var columns = new List<EditableColumnViewModel>(rows.Columns.Count);

        for (var ordinal = 0; ordinal < rows.Columns.Count; ordinal++)
        {
            var column = rows.Columns[ordinal];
            var position = ordinal;

            columns.Add(new EditableColumnViewModel(
                column,
                ResultColumnWidth.For(
                    column.Name,
                    column.DataType,
                    rows.Rows.Select(row => position < row.Count ? row[position] : null)),
                CanEdit));
        }

        return columns;
    }

    /// <summary>読めるだけの状態で開いた理由。書き換えられるときは null。</summary>
    private string? DescribeReadOnly(EditableRowSet rows)
    {
        if (IsReadOnlyConnection)
        {
            return "読み取り専用で開いている接続です。値は変更できません。";
        }

        if (!rows.HasKey)
        {
            return "行を 1 件に特定できる列が無いため、値は変更できません（主キーがありません）。";
        }

        return rows.HasEditableColumn ? null : "書き換えられる列がありません。";
    }

    private string DescribeStatus(EditableRowSet rows)
    {
        var count = rows.Rows.Count.ToString("N0", CultureInfo.InvariantCulture);

        return rows.IsTruncated
            ? $"{count} 行（{MaxRowsLabel}まで。これより後の行は読んでいません）"
            : $"{count} 行";
    }

    /// <summary>
    /// 待っている間に読み直されていないか。追い越された結果をそのまま出すと、
    /// 画面の行と書き戻した行が食い違う。
    /// </summary>
    private bool IsCurrent(int generation) => generation == _generation;
}
