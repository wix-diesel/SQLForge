using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Catalog;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ユーザーのプロパティ ダイアログ。SSMS の「データベース ユーザー」に合わせ、
/// 種類・ユーザー名・ログイン名・既定のスキーマと、メンバーシップ（ロール）を扱う。
///
/// 種類は作ったあとに変えられない（CREATE の時点で決まる）ので、編集では選ばせない。
/// </summary>
public sealed partial class DatabaseUserDialogViewModel : ObservableObject
{
    private readonly IDatabaseSession _session;
    private readonly DatabaseName _database;
    private readonly DatabaseUserDescriptor? _original;
    private readonly ListSchemasUseCase _schemas;
    private readonly ListDatabaseRolesUseCase _roles;
    private readonly SaveDatabaseUserUseCase _save;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _loginName;
    // 候補から選ばれていないときは ComboBox が null を書き戻すので、null を許す。
    [ObservableProperty] private string? _defaultSchema;
    [ObservableProperty] private DatabaseUserTypeChoiceViewModel _selectedType;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public DatabaseUserDialogViewModel(
        IDatabaseSession session,
        DatabaseName database,
        DatabaseUserDescriptor? original,
        ListSchemasUseCase schemas,
        ListDatabaseRolesUseCase roles,
        SaveDatabaseUserUseCase save)
    {
        _session = session;
        _database = database;
        _original = original;
        _schemas = schemas;
        _roles = roles;
        _save = save;

        var draft = original is null ? DatabaseUserDraft.ForNewUser() : DatabaseUserDraft.FromDescriptor(original);

        _name = draft.Name;
        _loginName = draft.LoginName;
        _defaultSchema = draft.DefaultSchema;
        TypeChoices = DatabaseUserTypes.Editable.Select(type => new DatabaseUserTypeChoiceViewModel(type)).ToList();

        // 一覧に無い種類（証明書ユーザーなど）で開かれても落ちないよう、無ければ先頭に落とす。
        _selectedType = TypeChoices.FirstOrDefault(choice => choice.Value == draft.Type) ?? TypeChoices[0];
    }

    public IReadOnlyList<DatabaseUserTypeChoiceViewModel> TypeChoices { get; }

    /// <summary>既定のスキーマの候補。手で書くこともできるので、あくまで候補。</summary>
    public ObservableCollection<string> SchemaChoices { get; } = [];

    /// <summary>メンバーシップの一覧。チェックの有無が所属の有無になる。</summary>
    public ObservableCollection<RoleChoiceViewModel> Roles { get; } = [];

    public bool IsNew => _original is null;

    public string Title => IsNew ? "新しいデータベース ユーザー" : $"データベース ユーザー — {_original!.Name.Value}";

    /// <summary>種類を選べるのは新規のときだけ。作ったあとは CREATE のやり直しになる。</summary>
    public bool CanChangeType => IsNew;

    public bool RequiresLogin => SelectedType.Value.RequiresLogin();

    public bool HasNameError => _validation[DatabaseUserValidator.NameField] is not null;

    public bool HasLoginError => _validation[DatabaseUserValidator.LoginField] is not null;

    public bool HasDefaultSchemaError => _validation[DatabaseUserValidator.DefaultSchemaField] is not null;

    public bool HasError => ErrorMessage is not null;

    /// <summary>閉じることを伝える。true なら保存済みで、呼び出し側は一覧を読み直す。</summary>
    public event EventHandler<bool>? CloseRequested;

    private SecurityValidationResult _validation = SecurityValidationResult.Valid;

    /// <summary>スキーマとロールの候補を読む。開いた直後に 1 度だけ呼ぶ。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadSchemasAsync(cancellationToken).ConfigureAwait(true);
            await LoadRolesAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 画面を閉じた。何もしない。
        }
        catch (Exception exception)
        {
            // 候補が読めなくても、名前と種類だけで作ることはできる。理由だけ出して開いたままにする。
            SetError(exception.Message);
        }
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
            var result = await _save.ExecuteAsync(_session, _database, ToDraft(), cancellationToken)
                .ConfigureAwait(true);

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
            // 権限不足や、同じ名前のユーザーが既にいる場合はここへ来る。開いたままにして理由を出す。
            SetError(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    private DatabaseUserDraft ToDraft() =>
        new()
        {
            Original = _original,
            Name = Name,
            Type = SelectedType.Value,
            LoginName = LoginName,
            DefaultSchema = DefaultSchema ?? string.Empty,
            Roles = Roles.Where(role => role.IsMember).Select(role => role.Name).ToList()
        };

    private async Task LoadSchemasAsync(CancellationToken cancellationToken)
    {
        var schemas = await _schemas.ExecuteAsync(_session, _database, cancellationToken).ConfigureAwait(true);

        foreach (var schema in schemas.Where(schema => !schema.IsSystem))
        {
            SchemaChoices.Add(schema.Name.Value);
        }

        // 今の値が候補に無いこともある（システムのスキーマを既定にしているユーザーなど）。
        if (DefaultSchema is { Length: > 0 } current && !SchemaChoices.Contains(current, StringComparer.Ordinal))
        {
            SchemaChoices.Insert(0, current);
        }
    }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _roles.ExecuteAsync(_session, _database, cancellationToken).ConfigureAwait(true);
        var current = _original?.Roles ?? [];

        foreach (var role in roles)
        {
            Roles.Add(new RoleChoiceViewModel(
                role,
                current.Contains(role, StringComparer.OrdinalIgnoreCase)));
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
        OnPropertyChanged(nameof(HasLoginError));
        OnPropertyChanged(nameof(HasDefaultSchemaError));
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnSelectedTypeChanged(DatabaseUserTypeChoiceViewModel value) =>
        OnPropertyChanged(nameof(RequiresLogin));
}
