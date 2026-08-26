using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.Presentation;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// データベース ロール 1 件。ツリーの葉で、右クリックからプロパティと削除を開く。
/// 変更が入ったら親の見出しごと読み直す（名前が変われば行の位置も変わるため）。
/// </summary>
public sealed partial class DatabaseRoleNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseSecurityContext _security;
    private readonly DatabaseName _database;
    private readonly DatabaseRoleDescriptor _descriptor;
    private readonly DatabaseRolesNode _owner;

    public DatabaseRoleNode(
        CatalogContext context,
        DatabaseSecurityContext security,
        DatabaseName database,
        DatabaseRoleDescriptor descriptor,
        DatabaseRolesNode owner)
        : base(descriptor.Name.Value, canExpand: false, isSystem: descriptor.IsSystem)
    {
        _context = context;
        _security = security;
        _database = database;
        _descriptor = descriptor;
        _owner = owner;
        Detail = DatabaseRoleDetailFormat.Describe(descriptor);
    }

    public RoleName Name => _descriptor.Name;

    public IReadOnlyList<string> Members => _descriptor.Members;

    /// <summary>
    /// プロパティを開いてよいか。固定ロール（db_owner など）も、メンバーの出し入れは
    /// できるので開ける。名前と所有者はダイアログの側で触らせない。
    /// </summary>
    public bool CanEdit => _security.RoleEditor is not null;

    /// <summary>削除してよいか。固定ロールは消せない。</summary>
    public bool CanDelete => CanEdit && _descriptor.IsEditable;

    /// <summary>右クリックの「プロパティ」。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task PropertiesAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.EditAsync(_context.Session, _database, _descriptor), cancellationToken);

    /// <summary>右クリックの「削除」。確認は行き先（ダイアログ）の受け持ち。</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.DeleteAsync(_context.Session, _database, _descriptor), cancellationToken);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);

    private async Task ApplyAsync(Func<IDatabaseRoleEditor, Task<bool>> action, CancellationToken cancellationToken)
    {
        if (_security.RoleEditor is not { } editor)
        {
            return;
        }

        if (await action(editor).ConfigureAwait(true))
        {
            await _owner.ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }
}
