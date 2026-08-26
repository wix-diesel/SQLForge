using SQLForge.Application.Abstractions;
using SQLForge.Application.Security;
using SQLForge.Domain.Catalog;
using SQLForge.Ui.ViewModels.Security;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの右クリックから呼ばれる <see cref="ISchemaEditor"/> の実装。
/// モーダルの出し方と確認の取り方は <see cref="SecurityDialogService"/> から借りる。
/// </summary>
public sealed class SchemaDialogService(
    ListDatabaseUsersUseCase users,
    ListDatabaseRolesUseCase roles,
    SaveSchemaUseCase save,
    DropSchemaUseCase drop) : SecurityDialogService, ISchemaEditor
{
    public Task<bool> CreateAsync(IDatabaseSession session, DatabaseName database) =>
        ShowEditorAsync(session, database, original: null);

    public Task<bool> EditAsync(IDatabaseSession session, DatabaseName database, SchemaDescriptor schema) =>
        ShowEditorAsync(session, database, schema);

    public async Task<bool> DeleteAsync(IDatabaseSession session, DatabaseName database, SchemaDescriptor schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var confirmed = await ConfirmAsync(
                $"スキーマ {schema.Name.Value} を削除しますか？",
                $"{database.Value} から削除します。テーブルなどが残っているスキーマは削除できません。"
                    + "この操作は取り消せません。")
            .ConfigureAwait(true);

        return confirmed
            && await TryDeleteAsync(() => drop.ExecuteAsync(session, database, schema)).ConfigureAwait(true);
    }

    private async Task<bool> ShowEditorAsync(
        IDatabaseSession session,
        DatabaseName database,
        SchemaDescriptor? original)
    {
        var dialog = new SchemaDialogViewModel(session, database, original, users, roles, save);
        var window = new SchemaWindow { DataContext = dialog };

        // 所有者の候補は開いてから読む。読めなくてもダイアログは出す。
        _ = dialog.InitializeAsync();

        dialog.CloseRequested += (_, result) => window.Close(result);

        return await ShowAsync(window).ConfigureAwait(true);
    }
}
