using SQLForge.Domain.Catalog;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>テーブル 1 件。ツリーの葉。</summary>
public sealed class TableNode : ObjectExplorerNode
{
    private readonly TableDescriptor _descriptor;

    public TableNode(TableDescriptor descriptor)
        : base(descriptor.Name, canExpand: false)
    {
        _descriptor = descriptor;
        Detail = RowCountFormat.Describe(descriptor.RowCount);
    }

    public string QualifiedName => _descriptor.QualifiedName;

    public long? RowCount => _descriptor.RowCount;

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);
}
