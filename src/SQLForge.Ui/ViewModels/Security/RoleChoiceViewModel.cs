using CommunityToolkit.Mvvm.ComponentModel;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ダイアログの「名前にチェックを付けて選ぶ」欄の 1 行。チェックの有無がそのまま所属の有無になる。
///
/// メンバーシップ（どのロールに入るか）・メンバー（誰を入れるか）・所有するスキーマは、
/// どれも見せ方が変わらないので器は 1 つにしてある。
/// </summary>
public sealed partial class RoleChoiceViewModel(string name, bool isMember) : ObservableObject
{
    [ObservableProperty] private bool _isMember = isMember;

    public string Name { get; } = name;
}
