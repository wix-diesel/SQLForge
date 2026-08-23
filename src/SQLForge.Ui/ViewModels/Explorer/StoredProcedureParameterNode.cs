using SQLForge.Domain.Catalog;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>ストアド プロシージャのパラメーター 1 件。ツリーの葉。</summary>
public sealed class StoredProcedureParameterNode : ObjectExplorerNode
{
    public StoredProcedureParameterNode(StoredProcedureParameterDescriptor descriptor)
        : base(descriptor.Name, canExpand: false)
    {
        IsOutput = descriptor.IsOutput;
        Detail = StoredProcedureParameterDetailFormat.Describe(descriptor);
    }

    /// <summary>OUTPUT パラメーターか。<see cref="Detail"/> の表示に使う。</summary>
    public bool IsOutput { get; }

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);
}
