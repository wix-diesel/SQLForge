using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Ui.ViewModels.Explorer;

namespace SQLForge.Ui.ViewModels;

/// <summary>
/// メインウィンドウのビューモデルを組み立てる。セッションだけは実行時にしか決まらないので、
/// DI から取れる残りをここで束ねておく。
/// </summary>
public sealed class MainWindowViewModelFactory(
    IPlatformProfile platform,
    ListDatabasesUseCase databases,
    ListSchemasUseCase schemas,
    ListTablesUseCase tables)
{
    public MainWindowViewModel Create(IDatabaseSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new MainWindowViewModel(session, platform, new CatalogContext(session, databases, schemas, tables));
    }
}
