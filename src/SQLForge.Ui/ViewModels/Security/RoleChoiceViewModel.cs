using CommunityToolkit.Mvvm.ComponentModel;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ダイアログのメンバーシップ欄に並ぶロール 1 つ。チェックの有無がそのまま所属の有無になる。
/// データベース ロールとサーバー ロールで見せ方は変わらないので、器は 1 つにしてある。
/// </summary>
public sealed partial class RoleChoiceViewModel(string name, bool isMember) : ObservableObject
{
    [ObservableProperty] private bool _isMember = isMember;

    public string Name { get; } = name;
}
