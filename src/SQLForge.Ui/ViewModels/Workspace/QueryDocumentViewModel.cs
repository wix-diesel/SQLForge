using System.Collections.ObjectModel;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Query;
using SQLForge.Domain.Sql;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// クエリエディタのタブ 1 枚。SSMS の「クエリ ウィンドウ」1 つにあたる。
///
/// 文面・実行先・実行の状態・結果ペインは、どれもこのタブだけのもの。
/// 隣のタブが実行していても、こちらの結果や見出しは巻き添えにならない。
/// タブを並べて開閉するのは <see cref="QueryEditorViewModel"/> の役目。
/// </summary>
public sealed partial class QueryDocumentViewModel : ObservableObject
{
    /// <summary>
    /// このタブを並べているタブ帯。閉じる系のメニューはタブの上に出るので、
    /// 押された合図を親へ返せるように持っておく（ツリーのノードが
    /// <see cref="Explorer.CatalogContext"/> を持つのと同じ形）。
    /// </summary>
    private readonly QueryEditorViewModel _owner;

    private readonly IDatabaseSession _session;
    private readonly ExecuteQueryUseCase _executeQuery;

    /// <summary>補完の候補を作る口。渡されないときは補完しない（既存のテストなど）。</summary>
    private readonly SqlCompletionUseCase? _completion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _targetDatabase = string.Empty;
    [ObservableProperty] private QueryTabViewModel? _selectedTab;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _hasFailed;

    /// <summary>打ち込んだあと保存していない。SSMS と同じで、見出しの後ろに * を付ける。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool _isModified;

    internal QueryDocumentViewModel(
        QueryEditorViewModel owner,
        string name,
        IDatabaseSession session,
        ExecuteQueryUseCase executeQuery,
        SqlCompletionUseCase? completion,
        DatabaseName? target)
    {
        _owner = owner;
        _session = session;
        _executeQuery = executeQuery;
        _completion = completion;
        Name = name;

        // 文面が変わったら「実行」「整形」の可否と、見出しの * を出し直す。
        Document.TextChanged += (_, _) => OnTextChanged();

        if (target is { } database)
        {
            Target = database;
            TargetDatabase = database.Value;
        }
    }

    /// <summary>タブの名前。SSMS と同じ SQLQuery1.sql の形で、閉じても番号は使い回さない。</summary>
    public string Name { get; }

    /// <summary>タブ帯に出す見出し。打ちかけの間は SSMS と同じで * を添える。</summary>
    public string Title => IsModified ? Name + "*" : Name;

    /// <summary>実行先。接続時に開いたデータベースか、ツリーで選んだデータベース。</summary>
    internal DatabaseName? Target { get; }

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

    /// <summary>結果ペインの「結果 1」…と「メッセージ」。実行のたびに作り直す。</summary>
    public ObservableCollection<QueryTabViewModel> Tabs { get; } = [];

    /// <summary>
    /// キャレットの位置。エディタは 1 つを使い回してタブごとに文書を差し替えるので、
    /// 戻ってきたときに書きかけの位置へ帰れるよう、タブ側で覚えておく。
    /// 出し入れするのはビュー（QueryWorkspacePane）だけ。
    /// </summary>
    public int CaretOffset { get; set; }

    /// <summary>
    /// キャレットの位置に出す補完の候補。実行先が決まっていないときや、
    /// 補完の口が無いときは空を返す（ビューはポップアップを出さない）。
    /// </summary>
    public async Task<SqlCompletionResult> CompleteAsync(
        int caret,
        CancellationToken cancellationToken = default)
    {
        if (_completion is null || Target is not { } database)
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

    /// <summary>走っている実行を取り消す。タブを閉じるときに親から呼ばれる。</summary>
    internal void CancelRun()
    {
        if (IsRunning)
        {
            RunCancelCommand.Execute(null);
        }
    }

    private bool CanRun => !IsRunning && !string.IsNullOrWhiteSpace(Sql);

    private bool CanFormat => !string.IsNullOrWhiteSpace(Sql);

    /// <summary>このタブを閉じる。</summary>
    [RelayCommand]
    private void Close() => _owner.Close(this);

    /// <summary>このタブ以外を閉じる（SSMS の「これ以外をすべて閉じる」）。</summary>
    [RelayCommand]
    private void CloseOthers() => _owner.CloseOthers(this);

    [RelayCommand]
    private void CloseAll() => _owner.CloseAll();

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
        IsModified = true;
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
        if (Target is not { } database)
        {
            ShowFailure("実行先のデータベースが決まっていません。左のツリーでデータベースかテーブルを選び直してください。");
            return;
        }

        IsRunning = true;

        try
        {
            var result = await _executeQuery
                .ExecuteAsync(_session, new QueryRequest(database, Sql), cancellationToken)
                .ConfigureAwait(true);

            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
            ShowFailure("実行を取り消しました。");
        }
        catch (Exception exception)
        {
            // 権限エラーも構文エラーもここへ来る。理由はサーバーの言葉のまま出す。
            ShowFailure(exception.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ShowResult(QueryResult result)
    {
        Tabs.Clear();

        for (var index = 0; index < result.ResultSets.Count; index++)
        {
            Tabs.Add(QueryTabViewModel.ForResult(index + 1, new QueryResultSetViewModel(result.ResultSets[index])));
        }

        Tabs.Add(QueryTabViewModel.ForMessages(QueryOutcomeFormat.Messages(result)));

        // 行が返ったならグリッド、返らなかったならメッセージが先頭に来る。
        SelectedTab = Tabs[0];
        Status = QueryOutcomeFormat.Status(result);
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
}
