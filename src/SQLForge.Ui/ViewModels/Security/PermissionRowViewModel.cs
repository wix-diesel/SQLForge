using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// 権限グリッドの 1 行。SSMS では「許可」「許可の付与」「拒否」の 3 つのチェックボックスだが、
/// 3 つのうち高々 1 つしか成り立たないので、ここでは 1 つの選択肢にまとめている。
/// </summary>
public sealed partial class PermissionRowViewModel : ObservableObject
{
    [ObservableProperty] private PermissionStateChoiceViewModel _selectedState;

    public PermissionRowViewModel(string permission, PermissionState state)
    {
        Permission = permission;
        _selectedState = PermissionStateChoiceViewModel.For(state);
    }

    /// <summary>権限の名前（SELECT・EXECUTE など）。</summary>
    public string Permission { get; }

    public IReadOnlyList<PermissionStateChoiceViewModel> StateChoices => PermissionStateChoiceViewModel.All;

    public PermissionState State => SelectedState.Value;
}
