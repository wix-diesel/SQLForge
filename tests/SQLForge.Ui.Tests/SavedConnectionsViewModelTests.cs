using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Ui.ViewModels;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>左ペインの読み込みと検索。</summary>
public class SavedConnectionsViewModelTests
{
    [Fact]
    public async Task 環境タグの順に見出しが並ぶ()
    {
        var viewModel = NewViewModel();

        await viewModel.LoadAsync();

        var headers = viewModel.Entries.OfType<ConnectionGroupHeaderViewModel>().Select(header => header.Title);
        Assert.Equal(["本番", "ステージング", "ローカル"], headers);
    }

    [Fact]
    public async Task 検索語で絞り込める()
    {
        var viewModel = NewViewModel();
        viewModel.SearchText = "staging";

        await viewModel.LoadAsync();

        var names = viewModel.Entries.OfType<SavedConnectionItemViewModel>().Select(item => item.Name);
        Assert.Equal(["staging-eu", "staging-warehouse"], names);
    }

    [Fact]
    public async Task 取り消された読み込みは一覧を書き換えない()
    {
        // 打鍵が速いときに、古い検索結果が新しい一覧を上書きしないこと。
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        var before = viewModel.Entries.Count;

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => viewModel.LoadAsync(cancellation.Token));
        Assert.Equal(before, viewModel.Entries.Count);
    }

    [Fact]
    public async Task 読み込みに失敗するとイベントで知らせる()
    {
        var viewModel = new SavedConnectionsViewModel(new ListSavedConnectionsUseCase(new FailingRepository()));
        Exception? reported = null;
        viewModel.LoadFailed += (_, exception) => reported = exception;

        viewModel.SearchText = "何か";
        await WaitUntil(() => reported is not null);

        Assert.IsType<InvalidOperationException>(reported);
        Assert.Empty(viewModel.Entries);
    }

    private static SavedConnectionsViewModel NewViewModel() =>
        new(new ListSavedConnectionsUseCase(new InMemoryConnectionProfileRepository()));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), "期待した状態になりませんでした。");
    }

    private sealed class FailingRepository : Application.Abstractions.IConnectionProfileRepository
    {
        public Task<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("接続情報を読めません。");

        public Task<ConnectionProfile?> FindAsync(ConnectionProfileId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ConnectionProfile?>(null);

        public Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(ConnectionProfileId id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
