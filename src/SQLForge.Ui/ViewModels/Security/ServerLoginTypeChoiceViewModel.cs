using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// ダイアログの「認証方式」の選択肢 1 つ。
/// 表示名を持たせるだけの器で、値そのものはドメインの列挙型。
/// </summary>
/// <param name="Value">この選択肢が表す種類。</param>
public sealed record ServerLoginTypeChoiceViewModel(ServerLoginType Value)
{
    public string DisplayName => Value.DisplayName();
}
