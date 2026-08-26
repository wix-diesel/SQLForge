using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// データベース ロールのプロパティ ダイアログ。SSMS の「データベース ロール」に合わせ、
/// ロール名・所有者・このロールが所有するスキーマ・このロールのメンバーを扱う。
///
/// 固定ロール（db_owner など）は名前と所有者を変えられないので、その 2 つは触らせない。
/// メンバーの出し入れは日常の操作なので、固定ロールでもできる。
/// </summary>
public sealed partial class DatabaseRoleDialogViewModel : SecurityDialogViewModel
{
    private readonly IDatabaseSession _session;
    private readonly DatabaseName _database;
    private readonly DatabaseRoleDescriptor? _original;
    private readonly ListDatabaseUsersUseCase _users;
    private readonly ListDatabaseRolesUseCase _roles;
    private readonly ListSchemasUseCase _schemas;
    private readonly SaveDatabaseRoleUseCase _save;
    private readonly SavePermissionsUseCase _savePermissions;

    [ObservableProperty] private string _name;
    // 候補から選ばれていないときは ComboBox が null を書き戻すので、null を許す。
    [ObservableProperty] private string? _owner;
    [ObservableProperty] private SecurityDialogPageViewModel _selectedPage;

    public DatabaseRoleDialogViewModel(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseRoleDescriptor? original,
        ListDatabaseUsersUseCase users,
        ListDatabaseRolesUseCase roles,
        ListSchemasUseCase schemas,
        SaveDatabaseRoleUseCase save,
        SavePermissionsUseCase savePermissions,
        SecurablePermissionsViewModel securables)
    {
        _session = session;
        _database = database;
        _original = original;
        _users = users;
        _roles = roles;
        _schemas = schemas;
        _save = save;
        _savePermissions = savePermissions;
        Securables = securables;

        var draft = original is null ? DatabaseRoleDraft.ForNewRole() : DatabaseRoleDraft.FromDescriptor(original);

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

    /// <summary>所有者の候補。ユーザーとロールのどちらもロールを持てる。</summary>
    public ObservableCollection<string> OwnerChoices { get; } = [];

    /// <summary>このロールのメンバー。チェックの有無がそのまま所属の有無になる。</summary>
    public ObservableCollection<RoleChoiceViewModel> Members { get; } = [];

    /// <summary>このロールが所有するスキーマ。外すと所有者は dbo へ移る。</summary>
    public ObservableCollection<RoleChoiceViewModel> Schemas { get; } = [];

    /// <summary>「セキュリティ保護可能なリソース」のページ。</summary>
    public SecurablePermissionsViewModel Securables { get; }

    public bool IsNew => _original is null;

    public string Title => IsNew ? "新しいデータベース ロール" : $"データベース ロール — {_original!.Name.Value}";

    /// <summary>名前・所有者・所有スキーマを触ってよいか。固定ロールでは触らせない。</summary>
    public bool CanEditDefinition => _original is null || _original.IsEditable;

    public bool HasNameError => ErrorFor(DatabaseRoleValidator.NameField) is not null;

    public bool HasOwnerError => ErrorFor(DatabaseRoleValidator.OwnerField) is not null;

    /// <summary>候補（ユーザー・ロール・スキーマ）と権限を読む。開いた直後に 1 度だけ呼ぶ。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadPrincipalsAsync(cancellationToken).ConfigureAwait(true);
            await LoadSchemasAsync(cancellationToken).ConfigureAwait(true);
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
        var result = await _save.ExecuteAsync(_session, _database, ToDraft(), cancellationToken)
            .ConfigureAwait(true);

        if (!result.IsValid)
        {
            return result;
        }

        // ロールができてからでないと権限は付けられない。名前を変えた編集では新しい名前で付ける。
        var principal = SecurityPrincipal.ForDatabaseRole(new RoleName(Name.Trim()));

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

    private DatabaseRoleDraft ToDraft() =>
        new()
        {
            Original = _original,
            Name = Name,
            Owner = Owner ?? string.Empty,
            Members = Members.Where(member => member.IsMember).Select(member => member.Name).ToList(),
            OwnedSchemas = Schemas.Where(schema => schema.IsMember).Select(schema => schema.Name).ToList()
        };

    /// <summary>
    /// 所有者とメンバーの候補。どちらもユーザーとロールから選ぶが、
    /// 自分自身をメンバーには入れられない（ロールが自分を含むことになる）。
    /// </summary>
    private async Task LoadPrincipalsAsync(CancellationToken cancellationToken)
    {
        var users = await _users.ExecuteAsync(_session, _database, cancellationToken).ConfigureAwait(true);
        var roles = await _roles.ExecuteAsync(_session, _database, cancellationToken).ConfigureAwait(true);

        var names = users.Select(user => user.Name.Value)
            .Concat(roles.Select(role => role.Name.Value))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in names)
        {
            OwnerChoices.Add(name);
        }

        var current = _original?.Members ?? [];

        foreach (var name in names.Where(name => !string.Equals(name, _original?.Name.Value, StringComparison.Ordinal)))
        {
            Members.Add(new RoleChoiceViewModel(name, current.Contains(name, StringComparer.OrdinalIgnoreCase)));
        }

        // 今の所有者が候補に無いこともある（読めない principal を持っているロールなど）。
        if (Owner is { Length: > 0 } owner && !OwnerChoices.Contains(owner, StringComparer.Ordinal))
        {
            OwnerChoices.Insert(0, owner);
        }
    }

    private async Task LoadSchemasAsync(CancellationToken cancellationToken)
    {
        var schemas = await _schemas.ExecuteAsync(_session, _database, cancellationToken).ConfigureAwait(true);
        var owned = _original?.OwnedSchemas ?? [];

        foreach (var schema in schemas.Where(schema => !schema.IsSystem))
        {
            Schemas.Add(new RoleChoiceViewModel(
                schema.Name.Value,
                owned.Contains(schema.Name.Value, StringComparer.OrdinalIgnoreCase)));
        }
    }
}
