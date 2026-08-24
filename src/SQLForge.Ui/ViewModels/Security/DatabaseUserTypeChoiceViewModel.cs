using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ダイアログの「ユーザーの種類」の選択肢 1 つ。
/// 表示名を持たせるだけの器で、値そのものはドメインの列挙型。
/// </summary>
/// <param name="Value">この選択肢が表す種類。</param>
public sealed record DatabaseUserTypeChoiceViewModel(DatabaseUserType Value)
{
    public string DisplayName => Value.DisplayName();
}
