using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「データベース ロール」の見出し。SSMS の [データベース] → [セキュリティ] → [ロール] →
/// [データベース ロール] にあたる。
/// 追加はこの見出しから、編集と削除は下のロール行から行う。
/// </summary>
public sealed partial class DatabaseRolesNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseSecurityContext _security;
    private readonly DatabaseName _database;

    public DatabaseRolesNode(CatalogContext context, DatabaseSecurityContext security, DatabaseName database)
        : base("データベース ロール", canExpand: true)
    {
        _context = context;
        _security = security;
        _database = database;
    }

    /// <summary>編集の行き先がつながっている構成か。押せるのに何も起きないメニューは出さない。</summary>
    public bool CanEdit => _security.RoleEditor is not null;

    /// <summary>右クリックの「新しいデータベース ロール」。作られたら一覧を読み直す。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task NewRoleAsync(CancellationToken cancellationToken)
    {
        if (_security.RoleEditor is not { } editor)
        {
            return;
        }

        if (await editor.CreateAsync(_context.Session, _database).ConfigureAwait(true))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>右クリックの「最新の情報に更新」。</summary>
    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken) => ReloadAsync(cancellationToken);

    protected override async Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(
        CancellationToken cancellationToken)
    {
        if (_security.Roles is not { } roles)
        {
            return [];
        }

        var loaded = await roles.ExecuteAsync(_context.Session, _database, cancellationToken).ConfigureAwait(true);

        return loaded.Select(role => new DatabaseRoleNode(_context, _security, _database, role, this)).ToList();
    }

    /// <summary>読み終えたら件数を見出しの右に出す（ほかの見出しと同じ）。</summary>
    protected override void OnChildrenLoaded(IReadOnlyList<ObjectExplorerNode> children) =>
        Detail = children.Count.ToString();

    protected override void OnChildrenFailed() => Detail = null;
}
