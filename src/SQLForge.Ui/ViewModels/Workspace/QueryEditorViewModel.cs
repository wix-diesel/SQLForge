using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Query;
using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// メインウィンドウ右の作業領域。クエリのタブを並べる帯。
///
/// タブ 1 枚が SSMS のクエリ ウィンドウ 1 つ（<see cref="QueryDocumentViewModel"/>）で、
/// 文面も実行先も結果もタブごとに持つ。ここが受け持つのは開く・切り替える・閉じるだけ。
/// タブが 1 枚も無い間は作業領域に何も出さない（<see cref="IsOpen"/>）。
/// </summary>
public sealed partial class QueryEditorViewModel : ObservableObject, IQueryLauncher
{
    private readonly IDatabaseSession _session;
    private readonly ExecuteQueryUseCase _executeQuery;

    /// <summary>補完の候補を作る口。渡されないときは補完しない（既存のテストなど）。</summary>
    private readonly SqlCompletionUseCase? _completion;

    /// <summary>接続時に開いたデータベース。ツールバーから新しいタブを開くときの既定の実行先。</summary>
    private readonly DatabaseName? _defaultTarget;

    /// <summary>
    /// 前に出した順（最後が今のタブ）。閉じたときにどれを前に出すかを決めるのに使う。
    /// SSMS と同じで、閉じた先は「隣」ではなく「直前に見ていたもの」。
    /// </summary>
    private readonly List<QueryDocumentViewModel> _activation = [];

    /// <summary>次に付ける見出しの番号。閉じても使い回さない（SSMS と同じ）。</summary>
    private int _nextNumber = 1;

    [ObservableProperty] private QueryDocumentViewModel? _selectedDocument;

    public QueryEditorViewModel(
        IDatabaseSession session,
        ExecuteQueryUseCase executeQuery,
        SqlCompletionUseCase? completion = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _executeQuery = executeQuery ?? throw new ArgumentNullException(nameof(executeQuery));
        _completion = completion;

        // 接続時に開いたデータベースを既定の実行先にしておく。名前として通らないときは
        // 実行先なしで開く（ツリーでデータベースを選べば決まる）。
        if (DatabaseName.TryCreate(session.Profile.Target.Database, out var opened))
        {
            _defaultTarget = opened;
        }
    }

    /// <summary>開いているクエリのタブ。左から開いた順に並ぶ。</summary>
    public ObservableCollection<QueryDocumentViewModel> Documents { get; } = [];

    /// <summary>作業領域を出すか。タブが 1 枚も無ければ畳んでおく。</summary>
    public bool IsOpen => Documents.Count > 0;

    public string MaxRowsLabel =>
        $"取得上限 {ExecuteQueryUseCase.DefaultMaxRows.ToString("N0", CultureInfo.InvariantCulture)} 行";

    /// <summary>そのデータベース向けの空のタブを 1 枚足す。</summary>
    public void OpenNewQuery(DatabaseName database) => Add(database);

    /// <summary>
    /// 「上位 1000 行を表示」など、ツリーが文面まで決めてくる入口。
    /// SSMS と同じで、開くたびに新しいタブが増える。
    /// </summary>
    public void OpenAndRunQuery(DatabaseName database, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("実行する文面は空にできません。", nameof(sql));
        }

        var document = Add(database);
        document.Sql = sql;
        document.RunCommand.Execute(null);
    }

    /// <summary>
    /// タブを 1 枚閉じる。走っている実行はそこで取り消す（結果を出す先が無くなるため）。
    /// </summary>
    internal void Close(QueryDocumentViewModel document)
    {
        var wasSelected = SelectedDocument == document;

        if (!Documents.Remove(document))
        {
            return;
        }

        _activation.Remove(document);
        document.CancelRun();

        // ビュー側の一覧が勝手に隣を選ぶことがあるので、閉じた後に選び直す。
        if (wasSelected || SelectedDocument == document)
        {
            SelectedDocument = _activation.Count > 0 ? _activation[^1] : null;
        }

        Changed();
    }

    /// <summary>押したタブ以外を閉じる（SSMS の「これ以外をすべて閉じる」）。</summary>
    internal void CloseOthers(QueryDocumentViewModel keep)
    {
        foreach (var document in Documents.Where(candidate => candidate != keep).ToList())
        {
            Close(document);
        }

        SelectedDocument = Documents.Contains(keep) ? keep : SelectedDocument;
    }

    internal void CloseAll()
    {
        foreach (var document in Documents.ToList())
        {
            Close(document);
        }
    }

    /// <summary>
    /// ツールバーの「新しいクエリ」。実行先は今のタブと同じ（無ければ接続時のデータベース）。
    /// </summary>
    [RelayCommand]
    private void NewDocument() => Add(SelectedDocument?.Target ?? _defaultTarget);

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void CloseSelected()
    {
        if (SelectedDocument is { } document)
        {
            Close(document);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSeveral))]
    private void NextDocument() => Step(1);

    [RelayCommand(CanExecute = nameof(HasSeveral))]
    private void PreviousDocument() => Step(-1);

    private bool HasDocument => Documents.Count > 0;

    private bool HasSeveral => Documents.Count > 1;

    /// <summary>タブを 1 枚足して前に出す。</summary>
    private QueryDocumentViewModel Add(DatabaseName? target)
    {
        var document = new QueryDocumentViewModel(
            this,
            $"SQLQuery{_nextNumber++}.sql",
            _session,
            _executeQuery,
            _completion,
            target);

        Documents.Add(document);
        SelectedDocument = document;
        Changed();

        return document;
    }

    /// <summary>端まで来たら反対の端へ回る（SSMS のタブの行き来と同じ）。</summary>
    private void Step(int offset)
    {
        if (SelectedDocument is not { } current)
        {
            return;
        }

        var index = Documents.IndexOf(current);
        var count = Documents.Count;

        SelectedDocument = Documents[((index + offset) % count + count) % count];
    }

    partial void OnSelectedDocumentChanged(QueryDocumentViewModel? value)
    {
        if (value is not null)
        {
            _activation.Remove(value);
            _activation.Add(value);
        }
    }

    /// <summary>タブの枚数が変わった。作業領域の出し分けとタブ操作の可否を出し直す。</summary>
    private void Changed()
    {
        OnPropertyChanged(nameof(IsOpen));
        CloseSelectedCommand.NotifyCanExecuteChanged();
        NextDocumentCommand.NotifyCanExecuteChanged();
        PreviousDocumentCommand.NotifyCanExecuteChanged();
    }
}
