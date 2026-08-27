using SQLForge.Application.Connections;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Security;
using SQLForge.Ui.ViewModels;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 左ペインの削除・書き出し・取り込み。尋ね事は差し替えて、
/// 「答えをどう扱うか」だけを確かめる。
/// </summary>
public class SavedConnectionManagementTests
{
    [Fact]
    public async Task 削除は確認を取ってから一覧から消す()
    {
        var (viewModel, context) = Setup([Profile("prod-sales"), Profile("local-dev", EnvironmentTag.Local)]);
        await viewModel.LoadAsync();
        var target = Row(viewModel, "prod-sales");

        await viewModel.DeleteAsync(target.Profile);

        Assert.Equal("prod-sales", Assert.Single(context.Prompt.DeleteRequests).Name);
        Assert.Equal(["local-dev"], viewModel.Entries.OfType<SavedConnectionItemViewModel>().Select(item => item.Name));
        Assert.True(context.LastOutcome?.Succeeded);
    }

    [Fact]
    public async Task 確認をやめれば消さない()
    {
        var (viewModel, context) = Setup([Profile("prod-sales")]);
        context.Prompt.ConfirmsDelete = false;
        await viewModel.LoadAsync();

        await viewModel.DeleteAsync(Row(viewModel, "prod-sales").Profile);

        Assert.Single(viewModel.Entries.OfType<SavedConnectionItemViewModel>());
        Assert.Null(context.LastOutcome);
    }

    [Fact]
    public async Task 行の右クリックからの書き出しはその接続だけを対象にする()
    {
        var (viewModel, context) = Setup([Profile("prod-sales"), Profile("local-dev", EnvironmentTag.Local)]);
        context.Prompt.ExportChoice = new ConnectionExportChoice("/tmp/export.toml", IncludeCredentials: false);
        await viewModel.LoadAsync();

        await Row(viewModel, "prod-sales").ExportCommand.ExecuteAsync(null);

        Assert.Equal("prod-sales", Assert.Single(context.Prompt.ExportTargets));
        Assert.Equal("prod-sales.toml", context.Prompt.SuggestedFileName);
        Assert.Equal("/tmp/export.toml", context.Archive.WrittenTo);
        Assert.Equal("prod-sales", Assert.Single(context.Archive.Written).Profile.Name);
    }

    [Fact]
    public async Task 接続名に区切り文字が混ざっても既定のファイル名にしない()
    {
        // 接続名をそのままファイル名にすると、区切り文字で置き場所が変わってしまう。
        var (viewModel, context) = Setup([Profile("prod/sales")]);
        await viewModel.LoadAsync();

        await viewModel.ExportAsync(Row(viewModel, "prod/sales").Profile);

        Assert.Equal("prodsales.toml", context.Prompt.SuggestedFileName);
    }

    [Fact]
    public async Task 書き出しをやめればファイルを作らない()
    {
        var (viewModel, context) = Setup([Profile("prod-sales")]);
        context.Prompt.ExportChoice = null;

        await viewModel.ExportAllAsync();

        Assert.Null(context.Archive.WrittenTo);
        Assert.Null(context.LastOutcome);
    }

    [Fact]
    public async Task すべて書き出しは保存済みのすべてを対象にする()
    {
        var (viewModel, context) = Setup([Profile("prod-sales"), Profile("local-dev", EnvironmentTag.Local)]);
        context.Prompt.ExportChoice = new ConnectionExportChoice("/tmp/all.toml", IncludeCredentials: true);

        await viewModel.ExportAllCommand.ExecuteAsync(null);

        Assert.Equal("すべての保存済み接続", Assert.Single(context.Prompt.ExportTargets));
        Assert.Equal(2, context.Archive.Written.Count);
        Assert.Contains("2 件", context.LastOutcome?.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 取り込みで当たったものは尋ねてから置き換える()
    {
        var existing = Profile("prod-sales");
        var (viewModel, context) = Setup([existing]);
        context.Prompt.ImportFile = "/tmp/export.toml";
        context.Prompt.ConflictAnswers.Enqueue(ImportConflictChoice.Replace);
        context.Archive.Stored =
        [
            new ArchivedConnection(Renamed(existing, "prod-sales-2"), null),
            new ArchivedConnection(Profile("local-dev", EnvironmentTag.Local), null)
        ];

        await viewModel.ImportCommand.ExecuteAsync(null);

        Assert.Equal(existing.Id, Assert.Single(context.Prompt.ConflictRequests).Id);
        var names = viewModel.Entries.OfType<SavedConnectionItemViewModel>().Select(item => item.Name).Order();
        Assert.Equal(["local-dev", "prod-sales-2"], names);
    }

    [Fact]
    public async Task すべて飛ばすを選ぶと以降は尋ねない()
    {
        var (viewModel, context) = Setup([Profile("prod-sales"), Profile("prod-warehouse")]);
        context.Prompt.ImportFile = "/tmp/export.toml";
        context.Prompt.ConflictAnswers.Enqueue(ImportConflictChoice.SkipAll);
        context.Archive.Stored =
        [
            new ArchivedConnection(Profile("prod-sales"), null),
            new ArchivedConnection(Profile("prod-warehouse"), null),
            new ArchivedConnection(Profile("local-dev", EnvironmentTag.Local), null)
        ];

        await viewModel.ImportAsync();

        Assert.Single(context.Prompt.ConflictRequests);
        Assert.Equal(3, viewModel.Entries.OfType<SavedConnectionItemViewModel>().Count());
        Assert.Contains("2 件は飛ばしました", context.LastOutcome?.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task すべて置き換えるを選ぶと以降は尋ねない()
    {
        var (viewModel, context) = Setup([Profile("prod-sales"), Profile("prod-warehouse")]);
        context.Prompt.ImportFile = "/tmp/export.toml";
        context.Prompt.ConflictAnswers.Enqueue(ImportConflictChoice.ReplaceAll);
        context.Archive.Stored =
        [
            new ArchivedConnection(Profile("prod-sales"), null),
            new ArchivedConnection(Profile("prod-warehouse"), null)
        ];

        await viewModel.ImportAsync();

        Assert.Single(context.Prompt.ConflictRequests);
        Assert.Contains("2 件を保存済み接続に足しました。", context.LastOutcome?.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 取り込みをキャンセルすると1件も足さない()
    {
        var (viewModel, context) = Setup([Profile("prod-sales")]);
        context.Prompt.ImportFile = "/tmp/export.toml";
        context.Prompt.ConflictAnswers.Enqueue(ImportConflictChoice.Cancel);
        context.Archive.Stored =
        [
            new ArchivedConnection(Profile("prod-sales"), null),
            new ArchivedConnection(Profile("local-dev", EnvironmentTag.Local), null)
        ];

        await viewModel.ImportAsync();
        await viewModel.LoadAsync();

        Assert.Equal(["prod-sales"], viewModel.Entries.OfType<SavedConnectionItemViewModel>().Select(item => item.Name));
        Assert.Null(context.LastOutcome);
    }

    [Fact]
    public async Task ファイルを選ばなければ何もしない()
    {
        var (viewModel, context) = Setup([]);
        context.Prompt.ImportFile = null;

        await viewModel.ImportAsync();

        Assert.Null(context.LastOutcome);
    }

    [Fact]
    public async Task 読めないファイルは理由を伝えるだけで終わる()
    {
        var (viewModel, context) = Setup([]);
        context.Prompt.ImportFile = "/tmp/broken.toml";
        context.Archive.ReadFailure = new FormatException("3 行目: 「キー = 値」の形になっていません。");

        await viewModel.ImportAsync();

        Assert.False(context.LastOutcome?.Succeeded);
        Assert.Equal("取り込めません", context.LastOutcome?.Headline);
        Assert.Contains("3 行目", context.LastOutcome?.Detail, StringComparison.Ordinal);
    }

    private static SavedConnectionItemViewModel Row(SavedConnectionsViewModel viewModel, string name) =>
        viewModel.Entries.OfType<SavedConnectionItemViewModel>().First(item => item.Name == name);

    private static (SavedConnectionsViewModel ViewModel, Context Context) Setup(IReadOnlyList<ConnectionProfile> profiles)
    {
        var repository = InMemoryConnectionProfileRepository.With(profiles);
        var store = new InMemorySecretStore();
        var context = new Context(new FakeConnectionArchive(), new FakeSavedConnectionPrompt());

        var viewModel = new SavedConnectionsViewModel(
            new ListSavedConnectionsUseCase(repository),
            new DeleteConnectionUseCase(repository, store),
            new ExportConnectionsUseCase(repository, store, context.Archive),
            new ImportConnectionsUseCase(repository, store, context.Archive),
            context.Prompt);

        viewModel.OperationCompleted += (_, outcome) => context.LastOutcome = outcome;

        return (viewModel, context);
    }

    private static ConnectionProfile Profile(string name, EnvironmentTag? environment = null) =>
        new(ConnectionProfileId.New(),
            name,
            environment ?? EnvironmentTag.Production,
            new ConnectionTarget(DatabaseDriver.SqlServer, new ServerAddress("db.internal", 1433), "sales_db", TlsMode.Require),
            new ConnectionCredentials("analyst_ro", AuthenticationMethod.Password, storeSecretInKeyring: true),
            AccessMode.ReadOnly);

    private static ConnectionProfile Renamed(ConnectionProfile profile, string name) =>
        new(profile.Id, name, profile.Environment, profile.Target, profile.Credentials, profile.AccessMode);

    private sealed record Context(FakeConnectionArchive Archive, FakeSavedConnectionPrompt Prompt)
    {
        public SavedConnectionOutcome? LastOutcome { get; set; }
    }
}
