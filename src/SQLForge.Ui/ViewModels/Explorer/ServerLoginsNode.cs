using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Filtering;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「ログイン」の見出し。SSMS の [サーバー] → [セキュリティ] → [ログイン] にあたる。
/// 追加はこの見出しから、編集と削除は下のログイン行から行う。
/// </summary>
public sealed partial class ServerLoginsNode : FolderNode
{
    private readonly CatalogContext _context;
    private readonly ServerSecurityContext _security;

    public ServerLoginsNode(CatalogContext context, ServerSecurityContext security)
        : base("ログイン", new ObjectFilterSpec([ObjectFilterProperty.Name], context.FilterEditor, "セキュリティ"))
    {
        _context = context;
        _security = security;
    }

    /// <summary>編集の行き先がつながっている構成か。押せるのに何も起きないメニューは出さない。</summary>
    public bool CanEdit => _security.Editor is not null;

    /// <summary>右クリックの「新しいログイン」。作られたら一覧を読み直す。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task NewLoginAsync(CancellationToken cancellationToken)
    {
        if (_security.Editor is not { } editor)
        {
            return;
        }

        if (await editor.CreateAsync(_context.Session).ConfigureAwait(true))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    protected override async Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(
        CancellationToken cancellationToken)
    {
        var logins = await _security.Logins.ExecuteAsync(_context.Session, cancellationToken).ConfigureAwait(true);

        return logins.Select(login => new ServerLoginNode(_context, _security, login, this)).ToList();
    }
}
