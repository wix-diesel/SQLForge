using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;
using SQLForge.Ui.Presentation;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// スキーマ 1 件。下にテーブルの見出しを持ち、右クリックからプロパティ（所有者）と削除を開く。
/// </summary>
public sealed partial class SchemaNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseName _database;
    private readonly SchemaDescriptor _descriptor;
    private readonly SchemasNode? _owner;

    /// <param name="owner">
    /// 変更のあとに読み直す見出し。ツリーだけを組むときは無くてよい。
    /// </param>
    public SchemaNode(
        CatalogContext context,
        DatabaseName database,
        SchemaDescriptor descriptor,
        SchemasNode? owner = null)
        : base(descriptor.Name.Value, canExpand: true, isSystem: descriptor.IsSystem)
    {
        _context = context;
        _database = database;
        _descriptor = descriptor;
        _owner = owner;
        Detail = SchemaDetailFormat.Describe(descriptor);
    }

    public SchemaName Name => _descriptor.Name;

    /// <summary>スキーマの所有者。読めないときは null。</summary>
    public string? Owner => _descriptor.Owner;

    /// <summary>
    /// 触ってよい相手か。システムのスキーマ（sys・INFORMATION_SCHEMA など）は
    /// 一覧に出すだけにする。
    /// </summary>
    public bool CanEdit => _context.Security?.SchemaEditor is not null && _descriptor.IsEditable;

    /// <summary>右クリックの「プロパティ」。所有者を付け替える。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task PropertiesAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.EditAsync(_context.Session, _database, _descriptor), cancellationToken);

    /// <summary>右クリックの「削除」。確認は行き先（ダイアログ）の受け持ち。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task DeleteAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.DeleteAsync(_context.Session, _database, _descriptor), cancellationToken);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
        [
            new CatalogFolderNode("テーブル", LoadTablesAsync),
            new CatalogFolderNode("ストアド プロシージャ", LoadStoredProceduresAsync)
        ]);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadTablesAsync(CancellationToken cancellationToken)
    {
        var tables = await _context.Tables
            .ExecuteAsync(_context.Session, _database, _descriptor.Name, cancellationToken)
            .ConfigureAwait(true);

        return tables.Select(table => new TableNode(_context, _database, table)).ToList();
    }

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadStoredProceduresAsync(CancellationToken cancellationToken)
    {
        var procedures = await _context.StoredProcedures
            .ExecuteAsync(_context.Session, _database, _descriptor.Name, cancellationToken)
            .ConfigureAwait(true);

        return procedures.Select(procedure => new StoredProcedureNode(_context, _database, procedure)).ToList();
    }

    private async Task ApplyAsync(Func<ISchemaEditor, Task<bool>> action, CancellationToken cancellationToken)
    {
        if (_context.Security?.SchemaEditor is not { } editor)
        {
            return;
        }

        if (await action(editor).ConfigureAwait(true) && _owner is { } owner)
        {
            await owner.ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }
}
