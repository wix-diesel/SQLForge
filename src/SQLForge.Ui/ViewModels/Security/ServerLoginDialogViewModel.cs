using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ログインのプロパティ ダイアログ。SSMS の「ログイン」に合わせ、
/// 認証方式・ログイン名・パスワードと規則・既定のデータベース（「全般」）、
/// サーバー ロールのメンバーシップ（「サーバー ロール」）、ユーザー マッピング、
/// セキュリティ保護可能なリソース、有効と無効（「状態」）を扱う。
///
/// 認証方式は作ったあとに変えられない（CREATE の時点で決まる）ので、編集では選ばせない。
/// </summary>
public sealed partial class ServerLoginDialogViewModel : ObservableObject
{
    private readonly IDatabaseSession _session;
    private readonly ServerLoginDescriptor? _original;
    private readonly ListDatabasesUseCase _databases;
    private readonly ListServerRolesUseCase _roles;
    private readonly SaveServerLoginUseCase _save;
    private readonly SavePermissionsUseCase _savePermissions;

    private SecurityValidationResult _validation = SecurityValidationResult.Valid;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _password;
    [ObservableProperty] private string _passwordConfirmation;
    [ObservableProperty] private bool _enforcePolicy;
    [ObservableProperty] private bool _enforceExpiration;
    [ObservableProperty] private bool _mustChangePassword;
    // 候補から選ばれていないときは ComboBox が null を書き戻すので、null を許す。
    [ObservableProperty] private string? _defaultDatabase;
    [ObservableProperty] private bool _isLoginEnabled;
    [ObservableProperty] private ServerLoginTypeChoiceViewModel _selectedType;
    [ObservableProperty] private SecurityDialogPageViewModel _selectedPage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public ServerLoginDialogViewModel(
        IDatabaseSession session,
        ServerLoginDescriptor? original,
        ListDatabasesUseCase databases,
        ListServerRolesUseCase roles,
        SaveServerLoginUseCase save,
        SavePermissionsUseCase savePermissions,
        LoginUserMappingsViewModel mapping,
        SecurablePermissionsViewModel securables)
    {
        _session = session;
        _original = original;
        _databases = databases;
        _roles = roles;
        _save = save;
        _savePermissions = savePermissions;
        Mapping = mapping;
        Securables = securables;

        var draft = original is null ? ServerLoginDraft.ForNewLogin() : ServerLoginDraft.FromDescriptor(original);

        _name = draft.Name;
        _password = draft.Password;
        _passwordConfirmation = draft.PasswordConfirmation;
        _enforcePolicy = draft.EnforcePolicy;
        _enforceExpiration = draft.EnforceExpiration;
        _mustChangePassword = draft.MustChangePassword;
        _defaultDatabase = draft.DefaultDatabase;
        _isLoginEnabled = !draft.IsDisabled;
        TypeChoices = ServerLoginTypes.Editable.Select(type => new ServerLoginTypeChoiceViewModel(type)).ToList();

        // 一覧に無い種類（証明書ログインなど）で開かれても落ちないよう、無ければ先頭に落とす。
        _selectedType = TypeChoices.FirstOrDefault(choice => choice.Value == draft.Type) ?? TypeChoices[0];

        Pages =
        [
            new SecurityDialogPageViewModel("全般", this),
            new SecurityDialogPageViewModel("ユーザー マッピング", Mapping),
            new SecurityDialogPageViewModel("セキュリティ保護可能なリソース", Securables)
        ];

        _selectedPage = Pages[0];
    }

    /// <summary>ページの並び。SSMS の「ページの選択」にあたる。</summary>
    public IReadOnlyList<SecurityDialogPageViewModel> Pages { get; }

    public IReadOnlyList<ServerLoginTypeChoiceViewModel> TypeChoices { get; }

    /// <summary>既定のデータベースの候補。手で書くこともできるので、あくまで候補。</summary>
    public ObservableCollection<string> DatabaseChoices { get; } = [];

    /// <summary>サーバー ロールの一覧。チェックの有無が所属の有無になる。</summary>
    public ObservableCollection<RoleChoiceViewModel> Roles { get; } = [];

    /// <summary>「ユーザー マッピング」のページ。</summary>
    public LoginUserMappingsViewModel Mapping { get; }

    /// <summary>「セキュリティ保護可能なリソース」のページ。</summary>
    public SecurablePermissionsViewModel Securables { get; }

    public bool IsNew => _original is null;

    public string Title => IsNew ? "新しいログイン" : $"ログイン — {_original!.Name.Value}";

    /// <summary>認証方式を選べるのは新規のときだけ。作ったあとは CREATE のやり直しになる。</summary>
    public bool CanChangeType => IsNew;

    /// <summary>
    /// 名前を変えられるか。Windows のログインは名前が Windows 側の principal の写しなので、
    /// SQL Server の側だけで付け替えることはできない。
    /// </summary>
    public bool CanRename => IsNew || !SelectedType.Value.IsWindows();

    /// <summary>パスワードと規則の欄を使う種類か。</summary>
    public bool RequiresPassword => SelectedType.Value.RequiresPassword();

    /// <summary>編集ではパスワード欄を空のままにできる（空なら今のパスワードが残る）。</summary>
    public bool CanKeepPassword => !IsNew && RequiresPassword;

    public bool HasNameError => _validation[ServerLoginValidator.NameField] is not null;

    public bool HasPasswordError => _validation[ServerLoginValidator.PasswordField] is not null;

    public bool HasConfirmationError => _validation[ServerLoginValidator.ConfirmationField] is not null;

    public bool HasPolicyError => _validation[ServerLoginValidator.PolicyField] is not null;

    public bool HasDefaultDatabaseError => _validation[ServerLoginValidator.DefaultDatabaseField] is not null;

    public bool HasError => ErrorMessage is not null;

    /// <summary>閉じることを伝える。true なら保存済みで、呼び出し側は一覧を読み直す。</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>データベースとサーバー ロールの候補を読む。開いた直後に 1 度だけ呼ぶ。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadDatabasesAsync(cancellationToken).ConfigureAwait(true);
            await LoadRolesAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた。何もしない。
        }
        catch (Exception exception)
        {
            // 候補が読めなくても、名前とパスワードだけで作ることはできる。理由だけ出して開いたままにする。
            SetError(exception.Message);
        }

        // ページはどちらも自分で失敗を受け止めるので、ここでは待つだけにする。
        await Mapping.InitializeAsync(cancellationToken).ConfigureAwait(true);
        await Securables.InitializeAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _save.ExecuteAsync(_session, ToDraft(), cancellationToken).ConfigureAwait(true);

            if (result.IsValid)
            {
                // ログインができてからでないと権限は付けられない。
                // 名前を変えた編集では新しい名前で付ける。
                var principal = SecurityPrincipal.ForLogin(new ServerLoginName(Name.Trim()));

                result = await _savePermissions
                    .ExecuteAsync(_session, Securables.ToDraft(principal), cancellationToken)
                    .ConfigureAwait(true);
            }

            SetValidation(result);

            if (result.IsValid)
            {
                CloseRequested?.Invoke(this, true);
            }
        }
        catch (OperationCanceledException)
        {
            ClearError();
        }
        catch (Exception exception)
        {
            // 権限不足や、同じ名前のログインが既にいる場合はここへ来る。開いたままにして理由を出す。
            SetError(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    private ServerLoginDraft ToDraft() =>
        new()
        {
            Original = _original,
            Name = Name,
            Type = SelectedType.Value,
            Password = Password,
            PasswordConfirmation = PasswordConfirmation,
            EnforcePolicy = EnforcePolicy,
            EnforceExpiration = EnforceExpiration,
            MustChangePassword = MustChangePassword,
            DefaultDatabase = DefaultDatabase ?? string.Empty,
            IsDisabled = !IsLoginEnabled,
            Roles = Roles.Where(role => role.IsMember).Select(role => role.Name).ToList(),
            OriginalMappings = Mapping.Original,
            Mappings = Mapping.ToDrafts()
        };

    private async Task LoadDatabasesAsync(CancellationToken cancellationToken)
    {
        // 既定のデータベースには master も選べる（むしろ SSMS の初期値）ので、システムも外さない。
        var databases = await _databases.ExecuteAsync(_session, cancellationToken).ConfigureAwait(true);

        foreach (var database in databases)
        {
            DatabaseChoices.Add(database.Name.Value);
        }

        // 今の値が候補に無いこともある（開けないデータベースを既定にしているログインなど）。
        if (DefaultDatabase is { Length: > 0 } current && !DatabaseChoices.Contains(current, StringComparer.Ordinal))
        {
            DatabaseChoices.Insert(0, current);
        }
    }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _roles.ExecuteAsync(_session, cancellationToken).ConfigureAwait(true);
        var current = _original?.Roles ?? [];

        foreach (var role in roles)
        {
            Roles.Add(new RoleChoiceViewModel(
                role.Name.Value,
                current.Contains(role.Name.Value, StringComparer.OrdinalIgnoreCase)));
        }
    }

    private void SetValidation(SecurityValidationResult validation)
    {
        _validation = validation;
        ErrorMessage = validation.FirstError;
        OnErrorChanged();
    }

    private void SetError(string message)
    {
        _validation = SecurityValidationResult.Valid;
        ErrorMessage = message;
        OnErrorChanged();
    }

    private void ClearError()
    {
        _validation = SecurityValidationResult.Valid;
        ErrorMessage = null;
        OnErrorChanged();
    }

    private void OnErrorChanged()
    {
        OnPropertyChanged(nameof(HasNameError));
        OnPropertyChanged(nameof(HasPasswordError));
        OnPropertyChanged(nameof(HasConfirmationError));
        OnPropertyChanged(nameof(HasPolicyError));
        OnPropertyChanged(nameof(HasDefaultDatabaseError));
        OnPropertyChanged(nameof(HasError));
    }

    /// <summary>認証方式で使う欄が変わる。どれを出すかは見え方の側で決める。</summary>
    partial void OnSelectedTypeChanged(ServerLoginTypeChoiceViewModel value)
    {
        OnPropertyChanged(nameof(RequiresPassword));
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(CanKeepPassword));
    }
}
