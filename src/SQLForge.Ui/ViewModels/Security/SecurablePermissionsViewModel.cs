using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// 「セキュリティ保護可能なリソース」のページ。SSMS の同名のページに合わせ、
/// 上にリソースの一覧、下に選んだリソースの権限グリッドを出す。
///
/// 主体（ログイン・ユーザー・ロール）が誰であっても見せ方は変わらないので、
/// どのプロパティ ダイアログからも同じものを差し込む。
/// </summary>
public sealed partial class SecurablePermissionsViewModel : ObservableObject
{
    private readonly IDatabaseSession _session;
    private readonly SecurityPrincipalKind _kind;
    private readonly string? _name;
    private readonly DatabaseName? _database;
    private readonly ListPermissionsUseCase _permissions;
    private readonly ListSecurablesUseCase _securables;

    /// <summary>一覧から外した行。差分に出すために「指定なし」の姿で控えておく。</summary>
    private readonly List<PermissionEntry> _removed = [];

    [ObservableProperty] private SecurableRowViewModel? _selectedSecurable;
    [ObservableProperty] private SecurableKindChoiceViewModel _selectedKind;
    [ObservableProperty] private SecurableReference? _selectedCandidate;
    [ObservableProperty] private bool _isLoading;

    /// <param name="kind">権限の持ち主の種類。何を付けられるのかがこれで決まる。</param>
    /// <param name="name">
    /// 権限の持ち主の名前。これから作る相手ではまだ決まっていないので null で、
    /// そのときは「今の権限」を読みにいかない（まだ 1 つも付いていない）。
    /// </param>
    /// <param name="database">
    /// データベース スコープの主体の居場所。サーバー スコープの主体では null。
    /// </param>
    public SecurablePermissionsViewModel(
        IDatabaseSession session,
        SecurityPrincipalKind kind,
        string? name,
        DatabaseName? database,
        ListPermissionsUseCase permissions,
        ListSecurablesUseCase securables)
    {
        _session = session;
        _kind = kind;
        _name = name;
        _database = database;
        _permissions = permissions;
        _securables = securables;

        KindChoices = kind.AvailableSecurables()
            .Select(securable => new SecurableKindChoiceViewModel(securable))
            .ToList();

        _selectedKind = KindChoices[0];
    }

    /// <summary>足せるリソースの種類。主体のスコープで決まる。</summary>
    public IReadOnlyList<SecurableKindChoiceViewModel> KindChoices { get; }

    /// <summary>グリッドに出ているリソース。</summary>
    public ObservableCollection<SecurableRowViewModel> Securables { get; } = [];

    /// <summary>選んだ種類のリソース候補。ここから選んで一覧へ足す。</summary>
    public ObservableCollection<SecurableReference> Candidates { get; } = [];

    /// <summary>開いたときにサーバーから読んだ姿。保存のときの突き合わせに使う。</summary>
    public IReadOnlyList<PermissionEntry> Original { get; private set; } = [];

    /// <summary>読み込みに失敗した理由。ページの中に出す。</summary>
    [ObservableProperty] private string? _error;

    public bool HasError => Error is not null;

    /// <summary>今の権限を読み、一覧を組み立てる。開いた直後に 1 度だけ呼ぶ。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            if (_name is { } name)
            {
                Original = await _permissions
                    .ExecuteAsync(_session, new SecurityPrincipal(_kind, name), _database, cancellationToken)
                    .ConfigureAwait(true);

                Fill(Original);
            }

            await LoadCandidatesAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた。何もしない。
        }
        catch (Exception exception)
        {
            // 新しく作る相手にはまだ権限が無いし、読む権限そのものが無いこともある。
            // どちらもページを開いたままにして、理由だけ出す。
            Error = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>今のグリッドの姿。保存はダイアログ側がまとめて行う。</summary>
    /// <param name="principal">
    /// 保存する時点での主体。名前を変えた編集では、新しい名前で権限を付け直す。
    /// </param>
    public PermissionDraft ToDraft(SecurityPrincipal principal) =>
        new()
        {
            Principal = principal,
            Database = _database,
            Original = Original,
            Entries = Securables
                .SelectMany(securable => securable.ToEntries())
                .Concat(_removed)
                .ToList()
        };

    /// <summary>選んだ種類の候補を読み直す。</summary>
    [RelayCommand]
    private async Task LoadCandidatesAsync(CancellationToken cancellationToken)
    {
        Candidates.Clear();

        try
        {
            var candidates = await _securables
                .ExecuteAsync(_session, SelectedKind.Value, _database, cancellationToken)
                .ConfigureAwait(true);

            foreach (var candidate in candidates)
            {
                Candidates.Add(candidate);
            }
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた。何もしない。
        }
        catch (Exception exception)
        {
            // 候補が読めなくても、すでに出ている行は編集できる。理由だけ出す。
            Error = exception.Message;
        }
    }

    /// <summary>選んだ候補をグリッドへ足す。すでにある行はそのまま選び直すだけにする。</summary>
    [RelayCommand]
    private void AddSecurable()
    {
        if (SelectedCandidate is not { } candidate)
        {
            return;
        }

        SelectedSecurable = Find(candidate) ?? Append(candidate);
    }

    /// <summary>
    /// 選んでいる行を一覧から外す。付いていた権限は「指定なし」に戻す行として残さないと、
    /// 外したつもりのものがサーバーに残る。
    /// </summary>
    [RelayCommand]
    private void RemoveSecurable()
    {
        if (SelectedSecurable is not { } row)
        {
            return;
        }

        foreach (var permission in row.Permissions)
        {
            permission.SelectedState = PermissionStateChoiceViewModel.For(PermissionState.Revoked);
        }

        SelectedSecurable = null;
        Securables.Remove(row);

        // 外した行の「指定なし」は、保存のときに REVOKE として出したい。
        // グリッドから消してしまうと差分に出てこないので、控えとして持っておく。
        _removed.AddRange(row.ToEntries());
    }

    private SecurableRowViewModel? Find(SecurableReference securable) =>
        Securables.FirstOrDefault(row => Same(row.Securable, securable));

    private SecurableRowViewModel Append(SecurableReference securable)
    {
        // 一度外した相手を足し直したときは控えを捨てる。残したままだと、
        // 後ろに並ぶ「指定なし」が、足し直したぶんの権限を打ち消してしまう。
        _removed.RemoveAll(entry => Same(entry.Securable, securable));

        var row = new SecurableRowViewModel(securable);
        Securables.Add(row);

        return row;
    }

    /// <summary>同じリソースを指しているか。</summary>
    private static bool Same(SecurableReference left, SecurableReference right) =>
        left.Kind == right.Kind
        && string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Schema, right.Schema, StringComparison.OrdinalIgnoreCase);

    /// <summary>読んだ権限をリソースごとにまとめ、グリッドの行に組み直す。</summary>
    private void Fill(IReadOnlyList<PermissionEntry> entries)
    {
        Securables.Clear();

        foreach (var group in entries.GroupBy(entry => entry.Securable.DisplayName, StringComparer.Ordinal))
        {
            var states = group.ToDictionary(
                entry => entry.Permission,
                entry => entry.State,
                StringComparer.OrdinalIgnoreCase);

            Securables.Add(new SecurableRowViewModel(group.First().Securable, states));
        }

        SelectedSecurable = Securables.FirstOrDefault();
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    // 種類を選び直すのは UI スレッドの操作なので、読み込みは投げっぱなしにする
    // （LoadCandidatesAsync が例外を受け止める）。
    partial void OnSelectedKindChanged(SecurableKindChoiceViewModel value) =>
        _ = LoadCandidatesAsync(CancellationToken.None);
}
