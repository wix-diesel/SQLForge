using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Security;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// サーバー ログイン 1 件。ツリーの葉で、右クリックからプロパティと削除を開く。
/// 変更が入ったら親の見出しごと読み直す（名前が変われば行の位置も変わるため）。
/// </summary>
public sealed partial class ServerLoginNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly ServerSecurityContext _security;
    private readonly ServerLoginDescriptor _descriptor;
    private readonly ServerLoginsNode _owner;

    public ServerLoginNode(
        CatalogContext context,
        ServerSecurityContext security,
        ServerLoginDescriptor descriptor,
        ServerLoginsNode owner)
        : base(descriptor.Name.Value, canExpand: false, isSystem: descriptor.IsSystem)
    {
        _context = context;
        _security = security;
        _descriptor = descriptor;
        _owner = owner;
        Detail = ServerLoginDetailFormat.Describe(descriptor);
    }

    public ServerLoginName Name => _descriptor.Name;

    public ServerLoginType Type => _descriptor.Type;

    public bool IsDisabled => _descriptor.IsDisabled;

    public IReadOnlyList<string> Roles => _descriptor.Roles;

    /// <summary>
    /// 触ってよい相手か。システムのログイン（sa など）と、この版が作り替えられない
    /// 種類（証明書にマップされたログインなど）は一覧に出すだけにする。
    /// </summary>
    public bool CanEdit => _security.Editor is not null && _descriptor.IsEditable;

    /// <summary>右クリックの「プロパティ」。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task PropertiesAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.EditAsync(_context.Session, _descriptor), cancellationToken);

    /// <summary>右クリックの「削除」。確認は行き先（ダイアログ）の受け持ち。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task DeleteAsync(CancellationToken cancellationToken) =>
        ApplyAsync(editor => editor.DeleteAsync(_context.Session, _descriptor), cancellationToken);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>([]);

    private async Task ApplyAsync(
        Func<Security.IServerLoginEditor, Task<bool>> action,
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
