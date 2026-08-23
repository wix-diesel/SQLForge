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
    /// 作業領域がつながっている構成か。ツリーだけを組むとき（テストなど）は
    /// 行き先が無いので、押せるのに何も起きないメニューを出さない。
    /// </summary>
    public bool CanQuery => _context.Query is not null;

    /// <summary>
    /// 右クリックの「クエリを実行」。このテーブルのあるデータベースを実行先にして、
    /// 空のエディタを開く。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void OpenQuery() => _context.Query?.OpenNewQuery(_database);
}
