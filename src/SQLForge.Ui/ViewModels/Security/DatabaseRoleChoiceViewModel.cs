using CommunityToolkit.Mvvm.ComponentModel;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ダイアログの「メンバーシップ」に並ぶロール 1 つ。
/// チェックの有無がそのまま所属の有無になる。
/// </summary>
public sealed partial class DatabaseRoleChoiceViewModel(string name, bool isMember) : ObservableObject
{
    [ObservableProperty] private bool _isMember = isMember;

    public string Name { get; } = name;
}
