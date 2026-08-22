using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>接続ダイアログ左ペイン。保存済み接続を検索し、環境タグごとに並べる。</summary>
public sealed partial class SavedConnectionsViewModel(ListSavedConnectionsUseCase listConnections) : ObservableObject
{
    private readonly ListSavedConnectionsUseCase _listConnections = listConnections;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SavedConnectionItemViewModel? _selectedItem;

    /// <summary>見出し行と接続行を混ぜた 1 本の一覧。見出しは選択できない。</summary>
    public ObservableCollection<IConnectionListEntry> Entries { get; } = [];

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>接続が選ばれたときに、その内容を入力欄へ写すためのイベント。</summary>
    public event EventHandler<ConnectionProfile>? ProfileSelected;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _listConnections.ExecuteAsync(SearchText, cancellationToken).ConfigureAwait(true);
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

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();

    partial void OnSelectedItemChanged(SavedConnectionItemViewModel? value)
    {
        if (value is not null)
        {
            ProfileSelected?.Invoke(this, value.Profile);
        }
    }
}
