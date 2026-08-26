using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Abstractions;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// スキーマのプロパティ ダイアログ。SSMS の「スキーマ」に合わせ、
/// スキーマ名とスキーマの所有者を扱う。
///
/// 名前は作ったあとに変えられない（エンジンにその文面が無い）ので、編集では触らせない。
/// </summary>
public sealed partial class SchemaDialogViewModel : SecurityDialogViewModel
{
    private readonly IDatabaseSession _session;
    private readonly DatabaseName _database;
    private readonly SchemaDescriptor? _original;
    private readonly ListDatabaseUsersUseCase _users;
    private readonly ListDatabaseRolesUseCase _roles;
    private readonly SaveSchemaUseCase _save;

    [ObservableProperty] private string _name;
    // 候補から選ばれていないときは ComboBox が null を書き戻すので、null を許す。
    [ObservableProperty] private string? _owner;

    public SchemaDialogViewModel(
        IDatabaseSession session,
        DatabaseName database,
        SchemaDescriptor? original,
        ListDatabaseUsersUseCase users,
        ListDatabaseRolesUseCase roles,
        SaveSchemaUseCase save)
    {
        _session = session;
        _database = database;
        _original = original;
        _users = users;
        _roles = roles;
        _save = save;

        var draft = original is null ? SchemaDraft.ForNewSchema() : SchemaDraft.FromDescriptor(original);

        _name = draft.Name;
        _owner = draft.Owner;
    }

    /// <summary>所有者の候補。ユーザーとロールのどちらもスキーマを持てる。</summary>
    public ObservableCollection<string> OwnerChoices { get; } = [];

    public bool IsNew => _original is null;

    public string Title => IsNew ? "新しいスキーマ" : $"スキーマ — {_original!.Name.Value}";

    /// <summary>名前を触ってよいか。作ったあとは変えられない。</summary>
    public bool CanChangeName => IsNew;

    public bool HasNameError => ErrorFor(SchemaValidator.NameField) is not null;

    public bool HasOwnerError => ErrorFor(SchemaValidator.OwnerField) is not null;

    /// <summary>所有者の候補を読む。開いた直後に 1 度だけ呼ぶ。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _users.ExecuteAsync(_session, _database, cancellationToken).ConfigureAwait(true);
            var roles = await _roles.ExecuteAsync(_session, _database, cancellationToken).ConfigureAwait(true);

            var names = users.Select(user => user.Name.Value)
                .Concat(roles.Select(role => role.Name.Value))
                .Order(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                OwnerChoices.Add(name);
            }

            // 今の所有者が候補に無いこともある（読めない principal が持っているスキーマなど）。
            if (Owner is { Length: > 0 } owner && !OwnerChoices.Contains(owner, StringComparer.Ordinal))
            {
                OwnerChoices.Insert(0, owner);
            }
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
    }

    protected override Task<SecurityValidationResult> SaveCoreAsync(CancellationToken cancellationToken) =>
        _save.ExecuteAsync(_session, _database, ToDraft(), cancellationToken);

    protected override void OnErrorChanged()
    {
        base.OnErrorChanged();
        OnPropertyChanged(nameof(HasNameError));
        OnPropertyChanged(nameof(HasOwnerError));
    }

    private SchemaDraft ToDraft() =>
        new()
        {
            Original = _original,
            Name = Name,
            Owner = Owner ?? string.Empty
        };
}
