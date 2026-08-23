using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>テーブル 1 件。ツリーの葉。右クリックからクエリエディタを開ける。</summary>
public sealed partial class TableNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseName _database;
    private readonly TableDescriptor _descriptor;

    public TableNode(CatalogContext context, DatabaseName database, TableDescriptor descriptor)
        : base(descriptor.Name, canExpand: false)
    {
        _context = context;
        _database = database;
        _descriptor = descriptor;
        Detail = RowCountFormat.Describe(descriptor.RowCount);
    }

    public string QualifiedName => _descriptor.QualifiedName;

    public long? RowCount => _descriptor.RowCount;

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);

    /// <summary>
    /// 右クリックの「クエリを実行」。作業領域にエディタを開き、このテーブルを見る文面を用意する。
    /// 実行はしない（何が走るか読んでから押せるように）。
    /// </summary>
    [RelayCommand]
    private void OpenQuery() =>
        _context.Query?.OpenTableQuery(_database, _descriptor.Schema, _descriptor.Name);
}
