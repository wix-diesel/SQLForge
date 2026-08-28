using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Query;
using SQLForge.Domain.Sql;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// メインウィンドウ右の作業領域。クエリエディタと結果ペインをまとめる。
///
/// 開いていない間は作業領域に何も出さない（<see cref="IsOpen"/>）。
/// エクスプローラーの右クリックで開くのは空のエディタで、実行先のデータベースだけを
/// 決めておく（テーブルの中身をのぞく文面は、別の入口として用意する）。
/// </summary>
public sealed partial class QueryEditorViewModel : ObservableObject, IQueryLauncher
{
    private readonly IDatabaseSession _session;
    private readonly ExecuteQueryUseCase _executeQuery;

    /// <summary>補完の候補を作る口。渡されないときは補完しない（既存のテストなど）。</summary>
    private readonly SqlCompletionUseCase? _completion;

    /// <summary>実行先。接続時に開いたデータベースを初期値にする。</summary>
    private DatabaseName? _target;

    /// <summary>
    /// エディタを開き直した回数。実行の最中に開き直されたかを見分けるのに使う。
    /// </summary>
    private int _generation;

    [ObservableProperty] private bool _isOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _targetDatabase = string.Empty;
    [ObservableProperty] private QueryTabViewModel? _selectedTab;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _hasFailed;

    public QueryEditorViewModel(
        IDatabaseSession session,
        ExecuteQueryUseCase executeQuery,
        SqlCompletionUseCase? completion = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _executeQuery = executeQuery ?? throw new ArgumentNullException(nameof(executeQuery));
        _completion = completion;

        // 文面が変わったら「実行」「整形」の可否を出し直す。
        Document.TextChanged += (_, _) => OnTextChanged();

        // 接続時に開いたデータベースを既定の実行先にしておく。名前として通らないときは
        // 実行先なしのまま開く（ツリーでデータベースを選べば決まる）。
        if (DatabaseName.TryCreate(session.Profile.Target.Database, out var opened))
        {
            SetTarget(opened);
        }
    }

    /// <summary>
    /// エディタの文面。AvaloniaEdit は文字列ではなく文書を編集するので、
    /// ビューモデルが持つのも文書にする（ビューモデルは最外層なので UI の型を持ってよい）。
    /// </summary>
    public TextDocument Document { get; } = new();

    /// <summary>実行する文面。文書の中身をそのまま指す。</summary>
    public string Sql
    {
        get => Document.Text;
        set => Document.Text = value;
    }

    /// <summary>「結果 1」…と「メッセージ」。実行のたびに作り直す。</summary>
    public ObservableCollection<QueryTabViewModel> Tabs { get; } = [];

    public string MaxRowsLabel =>
        $"取得上限 {ExecuteQueryUseCase.DefaultMaxRows.ToString("N0", CultureInfo.InvariantCulture)} 行";

    public void OpenNewQuery(DatabaseName database)
    {
        SetTarget(database);
        Sql = string.Empty;
        Open();
    }

    /// <summary>
    /// 「上位 1000 行を表示」など、ツリーが文面まで決めてくる入口。開いてすぐ実行する。
    /// </summary>
    public void OpenAndRunQuery(DatabaseName database, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("実行する文面は空にできません。", nameof(sql));
        }

        SetTarget(database);
        Sql = sql;
        Open();
        RunCommand.Execute(null);
    }

    /// <summary>
    /// キャレットの位置に出す補完の候補。実行先が決まっていないときや、
    /// 補完の口が無いときは空を返す（ビューはポップアップを出さない）。
    /// </summary>
    public async Task<SqlCompletionResult> CompleteAsync(
        int caret,
        CancellationToken cancellationToken = default)
    {
        if (_completion is null || _target is not { } database)
        {
            return SqlCompletionResult.Empty;
        }

        try
        {
            return await _completion
                .ExecuteAsync(database, Sql, caret, cancellationToken)
                .ConfigureAwait(true);
        }
        catch (Exception)
        {
            // 候補が読めないのは、書いている手を止める理由にならない。黙って出さない。
            return SqlCompletionResult.Empty;
        }
    }

    private bool CanRun => !IsRunning && !string.IsNullOrWhiteSpace(Sql);

    private bool CanFormat => !string.IsNullOrWhiteSpace(Sql);

    /// <summary>
    /// 文面を整える。字句の並びは変えないので、整えても実行の結果は変わらない。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanFormat))]
    private void Format()
    {
        var formatted = SqlFormatter.Format(Sql);

        if (!string.Equals(formatted, Sql, StringComparison.Ordinal))
        {
            Document.Replace(0, Document.TextLength, formatted);
        }
    }

    private void OnTextChanged()
    {
        OnPropertyChanged(nameof(Sql));
        RunCommand.NotifyCanExecuteChanged();
        FormatCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 文面を 1 回実行して結果ペインへ移す。失敗もメッセージのタブに出すだけで、
    /// 例外は外へ出さない（押した操作の結果は、押した場所の近くで見せる）。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun), IncludeCancelCommand = true)]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_target is not { } database)
        {
            ShowFailure("実行先のデータベースが決まっていません。左のツリーでデータベースかテーブルを選び直してください。");
            return;
        }

        var generation = _generation;
        IsRunning = true;

        try
        {
            var result = await _executeQuery
                .ExecuteAsync(_session, new QueryRequest(database, Sql), cancellationToken)
                .ConfigureAwait(true);

            if (IsCurrent(generation))
            {
                ShowResult(result);
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(generation))
            {
                ShowFailure("実行を取り消しました。");
            }
        }
        catch (Exception exception)
        {
            // 権限エラーも構文エラーもここへ来る。理由はサーバーの言葉のまま出す。
            if (IsCurrent(generation))
            {
                ShowFailure(exception.Message);
            }
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// 待っている間にエディタを開き直されていないか。
    ///
    /// 開き直した先はもう別のクエリなので、あとから返ってきた結果をそこへ出すと
    /// 「書いていない文の結果が出ている」ことになる。結果は捨てるだけで、
    /// 走っているクエリ自体は止めない（止めたいときは「停止」を押す）。
    /// </summary>
    private bool IsCurrent(int generation) => generation == _generation;

    /// <summary>作業領域を畳む。文面は残しておき、次に開いたときに続きから書けるようにする。</summary>
    [RelayCommand]
    private void Close() => IsOpen = false;

    private void Open()
    {
        _generation++;
        Tabs.Clear();
        SelectedTab = null;
        Status = string.Empty;
        HasFailed = false;
        IsOpen = true;
    }

    private void SetTarget(DatabaseName database)
    {
        _target = database;
        TargetDatabase = database.Value;
    }

    private void ShowResult(QueryResult result)
    {
        Tabs.Clear();

        for (var index = 0; index < result.ResultSets.Count; index++)
        {
            Tabs.Add(QueryTabViewModel.ForResult(index + 1, new QueryResultSetViewModel(result.ResultSets[index])));
        }

        Tabs.Add(QueryTabViewModel.ForMessages(DescribeMessages(result)));

        // 行が返ったならグリッド、返らなかったならメッセージが先頭に来る。
        SelectedTab = Tabs[0];
        Status = DescribeStatus(result);
        HasFailed = false;
    }

    private void ShowFailure(string message)
    {
        Tabs.Clear();
        Tabs.Add(QueryTabViewModel.ForMessages(message));

        SelectedTab = Tabs[0];
        Status = "エラー";
        HasFailed = true;
    }

    /// <summary>ツールバー右端の一行。実行時間と件数だけを出す。</summary>
    private static string DescribeStatus(QueryResult result)
    {
        var status = new StringBuilder(Duration(result.Elapsed));

        if (result.ResultSets.Count > 0)
        {
            status.Append(" · ").Append(Count(result.TotalRows)).Append(" 行");

            if (result.ResultSets.Any(set => set.IsTruncated))
            {
                status.Append("（上限で打ち切り）");
            }
        }
        else if (result.RecordsAffected >= 0)
        {
            status.Append(" · ").Append(Count(result.RecordsAffected)).Append(" 行処理");
        }

        return status.ToString();
    }

    /// <summary>「メッセージ」タブの本文。SSMS のメッセージ欄にあたる。</summary>
    private static string DescribeMessages(QueryResult result)
    {
        var lines = new List<string>();

        for (var index = 0; index < result.ResultSets.Count; index++)
        {
            var set = result.ResultSets[index];
            var label = result.ResultSets.Count > 1 ? $"結果 {index + 1}: " : string.Empty;

            lines.Add(set.IsTruncated
                ? $"{label}({Count(set.Rows.Count)} 行を取得しました。取得上限に達したため、これより後の行は読んでいません)"
                : $"{label}({Count(set.Rows.Count)} 行を取得しました)");
        }

        if (result.RecordsAffected >= 0)
        {
            lines.Add($"({Count(result.RecordsAffected)} 行処理されました)");
        }

        if (lines.Count == 0)
        {
            lines.Add("(行は返りませんでした)");
        }

        lines.Add(string.Empty);
        lines.Add($"実行時間: {Duration(result.Elapsed)}");

        return string.Join("\n", lines);
    }

    private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Duration(TimeSpan elapsed) =>
        $"{Math.Round(elapsed.TotalMilliseconds).ToString("N0", CultureInfo.InvariantCulture)} ms";
}
