using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Filtering;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「ユーザー」の見出し。SSMS の [データベース] → [セキュリティ] → [ユーザー] にあたる。
/// 追加はこの見出しから、編集と削除は下のユーザー行から行う。
/// </summary>
public sealed partial class DatabaseUsersNode : FolderNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseSecurityContext _security;
    private readonly DatabaseName _database;

    public DatabaseUsersNode(CatalogContext context, DatabaseSecurityContext security, DatabaseName database)
        : base(
            "ユーザー",
            new ObjectFilterSpec(
                [ObjectFilterProperty.Name], context.FilterEditor, $"{database.Value}/セキュリティ"))
    {
        _context = context;
        _security = security;
        _database = database;
    }

    /// <summary>編集の行き先がつながっている構成か。押せるのに何も起きないメニューは出さない。</summary>
    public bool CanEdit => _security.Editor is not null;

    /// <summary>右クリックの「新しいユーザー」。作られたら一覧を読み直す。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task NewUserAsync(CancellationToken cancellationToken)
    {
        if (_security.Editor is not { } editor)
        {
            return;
        }

        if (await editor.CreateAsync(_context.Session, _database).ConfigureAwait(true))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    protected override async Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(
        CancellationToken cancellationToken)
    {
        var users = await _security.Users
            .ExecuteAsync(_context.Session, _database, cancellationToken)
            .ConfigureAwait(true);

        return users.Select(user => new DatabaseUserNode(_context, _security, _database, user, this)).ToList();
    }
}
