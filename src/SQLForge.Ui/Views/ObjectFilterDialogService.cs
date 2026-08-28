using SQLForge.Domain.Filtering;
using SQLForge.Ui.ViewModels.Explorer;

namespace SQLForge.Ui.Views;

/// <summary>
/// ツリーの右クリックから呼ばれる <see cref="IObjectFilterEditor"/> の実装。
/// モーダルの出し方は <see cref="SecurityDialogService"/> から借りる。
/// </summary>
public sealed class ObjectFilterDialogService : SecurityDialogService, IObjectFilterEditor
{
    public async Task<ObjectFilter?> EditAsync(
        string path,
        IReadOnlyList<ObjectFilterProperty> properties,
        ObjectFilter current)
    {
        var dialog = new ObjectFilterDialogViewModel(path, properties, current);
        var window = new ObjectFilterWindow { DataContext = dialog };

        dialog.CloseRequested += (_, result) => window.Close(result);

        // OK でなければ（キャンセル・窓の × で閉じた）今の条件のままにする。
        return await ShowAsync(window).ConfigureAwait(true) ? dialog.Result : null;
    }
}
