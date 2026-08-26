using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Security;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// サーバー ロールのプロパティ ダイアログ。SSMS の「サーバー ロール」に合わせ、
/// ロール名・所有者・メンバー・メンバーシップを扱う。
///
/// 固定ロール（sysadmin など）は名前・所有者・メンバーシップを変えられないので、
/// そこは触らせない。メンバーの出し入れは日常の操作なのでできる。
/// </summary>
public sealed partial class ServerRoleDialogViewModel : SecurityDialogViewModel
{
    private readonly IDatabaseSession _session;
    private readonly ServerRoleDescriptor? _original;
    private readonly ListServerLoginsUseCase _logins;
    private readonly ListServerRolesUseCase _roles;
    private readonly SaveServerRoleUseCase _save;
    private readonly SavePermissionsUseCase _savePermissions;

    [ObservableProperty] private string _name;
    // 候補から選ばれていないときは ComboBox が null を書き戻すので、null を許す。
    [ObservableProperty] private string? _owner;
    [ObservableProperty] private SecurityDialogPageViewModel _selectedPage;

    public ServerRoleDialogViewModel(
        IDatabaseSession session,
        ServerRoleDescriptor? original,
        ListServerLoginsUseCase logins,
        ListServerRolesUseCase roles,
        SaveServerRoleUseCase save,
        SavePermissionsUseCase savePermissions,
        SecurablePermissionsViewModel securables)
    {
        _session = session;
        _original = original;
        _logins = logins;
        _roles = roles;
        _save = save;
        _savePermissions = savePermissions;
        Securables = securables;

        var draft = original is null ? ServerRoleDraft.ForNewRole() : ServerRoleDraft.FromDescriptor(original);

        _name = draft.Name;
        _owner = draft.Owner;

        Pages =
        [
            new SecurityDialogPageViewModel("全般", this),
            new SecurityDialogPageViewModel("セキュリティ保護可能なリソース", Securables)
        ];

        _selectedPage = Pages[0];
    }

    /// <summary>ページの並び。SSMS の「ページの選択」にあたる。</summary>
    public IReadOnlyList<SecurityDialogPageViewModel> Pages { get; }

    /// <summary>所有者の候補。ログインとサーバー ロールのどちらもロールを持てる。</summary>
    public ObservableCollection<string> OwnerChoices { get; } = [];

    /// <summary>このロールのメンバー。チェックの有無がそのまま所属の有無になる。</summary>
    public ObservableCollection<RoleChoiceViewModel> Members { get; } = [];

    /// <summary>このロールが入る別のサーバー ロール。</summary>
    public ObservableCollection<RoleChoiceViewModel> Memberships { get; } = [];

    /// <summary>「セキュリティ保護可能なリソース」のページ。</summary>
    public SecurablePermissionsViewModel Securables { get; }

    public bool IsNew => _original is null;

    public string Title => IsNew ? "新しいサーバー ロール" : $"サーバー ロール — {_original!.Name.Value}";

    /// <summary>名前・所有者・メンバーシップを触ってよいか。固定ロールでは触らせない。</summary>
    public bool CanEditDefinition => _original is null || _original.IsEditable;

    public bool HasNameError => ErrorFor(ServerRoleValidator.NameField) is not null;

    public bool HasOwnerError => ErrorFor(ServerRoleValidator.OwnerField) is not null;

    /// <summary>候補（ログイン・ロール）と権限を読む。開いた直後に 1 度だけ呼ぶ。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadPrincipalsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた。何もしない。
        }
        catch (Exception exception)
        {
            // 候補が読めなくても、名前だけで作ることはできる。理由だけ出して開いたままにする。
            SetError(exception.Message);
        }

        // 権限のページは自分で失敗を受け止めるので、ここでは待つだけにする。
        await Securables.InitializeAsync(cancellationToken).ConfigureAwait(true);
    }

    protected override async Task<SecurityValidationResult> SaveCoreAsync(CancellationToken cancellationToken)
    {
        var result = await _save.ExecuteAsync(_session, ToDraft(), cancellationToken).ConfigureAwait(true);

        if (!result.IsValid)
        {
            return result;
        }

        // ロールができてからでないと権限は付けられない。名前を変えた編集では新しい名前で付ける。
        var principal = SecurityPrincipal.ForServerRole(new RoleName(Name.Trim()));

        return await _savePermissions
            .ExecuteAsync(_session, Securables.ToDraft(principal), cancellationToken)
            .ConfigureAwait(true);
    }

    protected override void OnErrorChanged()
    {
        base.OnErrorChanged();
        OnPropertyChanged(nameof(HasNameError));
        OnPropertyChanged(nameof(HasOwnerError));
    }

    private ServerRoleDraft ToDraft() =>
        new()
        {
            Original = _original,
            Name = Name,
            Owner = Owner ?? string.Empty,
            Members = Members.Where(member => member.IsMember).Select(member => member.Name).ToList(),
            Memberships = Memberships.Where(role => role.IsMember).Select(role => role.Name).ToList()
        };

    private async Task LoadPrincipalsAsync(CancellationToken cancellationToken)
    {
        var logins = await _logins.ExecuteAsync(_session, cancellationToken).ConfigureAwait(true);
        var roles = await _roles.ExecuteAsync(_session, cancellationToken).ConfigureAwait(true);

        var names = logins.Select(login => login.Name.Value)
            .Concat(roles.Select(role => role.Name.Value))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in names)
        {
            OwnerChoices.Add(name);
        }

        var members = _original?.Members ?? [];

        // 自分自身をメンバーにも、自分自身への所属にもできない。
        foreach (var name in names.Where(name => !string.Equals(name, _original?.Name.Value, StringComparison.Ordinal)))
        {
            Members.Add(new RoleChoiceViewModel(name, members.Contains(name, StringComparer.OrdinalIgnoreCase)));
        }

        var memberships = _original?.Memberships ?? [];

        foreach (var role in roles.Where(role =>
            !string.Equals(role.Name.Value, _original?.Name.Value, StringComparison.Ordinal)))
        {
            Memberships.Add(new RoleChoiceViewModel(
                role.Name.Value,
                memberships.Contains(role.Name.Value, StringComparer.OrdinalIgnoreCase)));
        }

        // 今の所有者が候補に無いこともある（読めない principal を持っているロールなど）。
        if (Owner is { Length: > 0 } owner && !OwnerChoices.Contains(owner, StringComparer.Ordinal))
        {
            OwnerChoices.Insert(0, owner);
        }
    }
}
