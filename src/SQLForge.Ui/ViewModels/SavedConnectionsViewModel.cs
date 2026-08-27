using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// 接続ダイアログ左ペイン。保存済み接続を検索し、環境タグごとに並べる。
/// 削除・書き出し・取り込みは <c>SavedConnectionsViewModel.Management.cs</c> にある。
/// </summary>
public sealed partial class SavedConnectionsViewModel : ObservableObject
{
    /// <summary>打鍵ごとに読み直さないための待ち時間。</summary>
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(180);

    private readonly ListSavedConnectionsUseCase _listConnections;
    private readonly DeleteConnectionUseCase _deleteConnection;
    private readonly ExportConnectionsUseCase _exportConnections;
    private readonly ImportConnectionsUseCase _importConnections;
    private readonly ISavedConnectionPrompt _prompt;
    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SavedConnectionItemViewModel? _selectedItem;

    public SavedConnectionsViewModel(
        ListSavedConnectionsUseCase listConnections,
        DeleteConnectionUseCase deleteConnection,
        ExportConnectionsUseCase exportConnections,
        ImportConnectionsUseCase importConnections,
        ISavedConnectionPrompt prompt)
    {
        _listConnections = listConnections;
        _deleteConnection = deleteConnection;
        _exportConnections = exportConnections;
        _importConnections = importConnections;
        _prompt = prompt;
    }

    /// <summary>見出し行と接続行を混ぜた 1 本の一覧。見出しは選択できない。</summary>
    public ObservableCollection<IConnectionListEntry> Entries { get; } = [];

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>接続が選ばれたときに、その内容を入力欄へ写すためのイベント。</summary>
    public event EventHandler<ConnectionProfile>? ProfileSelected;

    /// <summary>
    /// 利用者が行を押したときに、その接続を開くためのイベント。
    /// 選択が変わっただけ（起動直後の選び直しやキーボードでの移動）では起こさない。
    /// 開くつもりのない接続へ繋いでしまわないよう、押した操作だけを合図にする。
    /// </summary>
    public event EventHandler<ConnectionProfile>? ProfileActivated;

    /// <summary>検索の読み直しが失敗したことを伝える。呼び出し側でステータス表示に落とす。</summary>
    public event EventHandler<Exception>? LoadFailed;

    /// <summary>行が押された。入力欄へ写したうえで、その接続を開くよう求める。</summary>
    public void Activate(SavedConnectionItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // すでに選ばれている行を押し直したときは、入力欄をそのままにする
        // （打ちかけのパスワードを消さないため）。
        SelectedItem = item;

        ProfileActivated?.Invoke(this, item.Profile);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _listConnections.ExecuteAsync(SearchText, cancellationToken).ConfigureAwait(true);

        // 遅れて返ってきた古い検索結果で一覧を上書きしない。
        cancellationToken.ThrowIfCancellationRequested();

        var previous = SelectedItem?.Profile.Id;
        Rebuild(groups);
        Restore(previous);
    }

    private void Rebuild(IReadOnlyList<SavedConnectionGroup> groups)
    {
        Entries.Clear();

        foreach (var group in groups)
        {
            Entries.Add(new ConnectionGroupHeaderViewModel(group.Environment));

            foreach (var profile in group.Profiles)
            {
                Entries.Add(new SavedConnectionItemViewModel(profile, this));
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    private void Restore(ConnectionProfileId? previous)
    {
        var items = Entries.OfType<SavedConnectionItemViewModel>().ToList();
        SelectedItem = items.FirstOrDefault(item => item.Profile.Id == previous) ?? items.FirstOrDefault();
    }

    partial void OnSearchTextChanged(string value) => _ = ReloadForSearchAsync();

    /// <summary>
    /// 検索欄の変更に追従する。打鍵のたびに走らせると、同時実行と結果の追い越しで
    /// 一覧が古い内容に戻りうるので、直前の読み直しを取り消してから始める。
    /// </summary>
    private async Task ReloadForSearchAsync()
    {
        var cancellation = new CancellationTokenSource();
        Cancel(Interlocked.Exchange(ref _searchCancellation, cancellation));

        try
        {
            await Task.Delay(SearchDebounce, cancellation.Token).ConfigureAwait(true);
            await LoadAsync(cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 次の打鍵に追い越された。何もしない。
        }
        catch (Exception exception)
        {
            LoadFailed?.Invoke(this, exception);
        }
    }

    /// <summary>
    /// 走っている検索の読み直しを取り消す。削除や取り込みのあとに読み直すとき、
    /// 遅れて返ってきた検索結果で一覧を上書きされないようにするため
    /// （消したはずの行が戻って見える）。
    /// </summary>
    private void CancelSearchReload() => Cancel(Interlocked.Exchange(ref _searchCancellation, null));

    private static void Cancel(CancellationTokenSource? cancellation)
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    partial void OnSelectedItemChanged(SavedConnectionItemViewModel? value)
    {
        if (value is not null)
        {
            ProfileSelected?.Invoke(this, value.Profile);
        }
    }
}
