using SQLForge.Domain.Catalog;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>カラム定義 1 件。ツリーの葉。</summary>
public sealed class ColumnNode : ObjectExplorerNode
{
    public ColumnNode(ColumnDescriptor descriptor)
        : base(descriptor.Name, canExpand: false)
    {
        IsPrimaryKey = descriptor.IsPrimaryKey;
        Detail = ColumnDetailFormat.Describe(descriptor);
    }

    /// <summary>主キーの一部か。ツリーのアイコンを鍵に差し替えるのに使う。</summary>
    public bool IsPrimaryKey { get; }

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);
}
