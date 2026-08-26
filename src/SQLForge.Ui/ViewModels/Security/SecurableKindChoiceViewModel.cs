using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// 「セキュリティ保護可能なリソースの追加」で選ぶ種類 1 つ。
/// 表示名を持たせるだけの器で、値そのものはドメインの列挙型。
/// </summary>
/// <param name="Value">この選択肢が表す種類。</param>
public sealed record SecurableKindChoiceViewModel(SecurableKind Value)
{
    public string DisplayName => Value.DisplayName();
}
