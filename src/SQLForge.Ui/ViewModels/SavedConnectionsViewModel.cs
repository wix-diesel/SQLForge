using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>接続ダイアログ左ペイン。保存済み接続を検索し、環境タグごとに並べる。</summary>
public sealed partial class SavedConnectionsViewModel(ListSavedConnectionsUseCase listConnections) : ObservableObject
{
    /// <summary>打鍵ごとに読み直さないための待ち時間。</summary>
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(180);

    private readonly ListSavedConnectionsUseCase _listConnections = listConnections;
    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SavedConnectionItemViewModel? _selectedItem;

    /// <summary>見出し行と接続行を混ぜた 1 本の一覧。見出しは選択できない。</summary>
    public ObservableCollection<IConnectionListEntry> Entries { get; } = [];

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>接続が選ばれたときに、その内容を入力欄へ写すためのイベント。</summary>
    public event EventHandler<ConnectionProfile>? ProfileSelected;

    /// <summary>検索の読み直しが失敗したことを伝える。呼び出し側でステータス表示に落とす。</summary>
    public event EventHandler<Exception>? LoadFailed;

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
                Entries.Add(new SavedConnectionItemViewModel(profile));
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
