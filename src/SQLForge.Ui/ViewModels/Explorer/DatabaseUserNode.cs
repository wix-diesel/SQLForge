using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// データベース ユーザー 1 件。ツリーの葉で、右クリックからプロパティと削除を開く。
/// 変更が入ったら親の見出しごと読み直す（名前が変われば行の位置も変わるため）。
/// </summary>
public sealed partial class DatabaseUserNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseSecurityContext _security;
    private readonly DatabaseName _database;
    private readonly DatabaseUserDescriptor _descriptor;
    private readonly DatabaseUsersNode _owner;

    public DatabaseUserNode(
        CatalogContext context,
        DatabaseSecurityContext security,
        DatabaseName database,
        DatabaseUserDescriptor descriptor,
        DatabaseUsersNode owner)
        : base(descriptor.Name.Value, canExpand: false, isSystem: descriptor.IsSystem)
    {
        _context = context;
        _security = security;
        _database = database;
        _descriptor = descriptor;
        _owner = owner;
        Detail = DatabaseUserDetailFormat.Describe(descriptor);
    }

    public DatabaseUserName Name => _descriptor.Name;

    public DatabaseUserType Type => _descriptor.Type;

    public IReadOnlyList<string> Roles => _descriptor.Roles;

    /// <summary>
    /// 触ってよい相手か。システムのユーザー（dbo・guest・sys）と、この版が作り替えられない
    /// 種類（証明書にマップされたユーザーなど）は一覧に出すだけにする。
    /// </summary>
    public bool CanEdit => _security.Editor is not null && _descriptor.IsEditable;

    /// <summary>右クリックの「プロパティ」。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task PropertiesAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.EditAsync(_context.Session, _database, _descriptor), cancellationToken);

    /// <summary>右クリックの「削除」。確認は行き先（ダイアログ）の受け持ち。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task DeleteAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.DeleteAsync(_context.Session, _database, _descriptor), cancellationToken);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);

    private async Task ApplyAsync(
        Func<Security.IDatabaseUserEditor, Task<bool>> action,
        CancellationToken cancellationToken)
    {
        if (_security.Editor is not { } editor)
        {
            return;
        }

        if (await action(editor).ConfigureAwait(true))
        {
            await _owner.ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }
}
