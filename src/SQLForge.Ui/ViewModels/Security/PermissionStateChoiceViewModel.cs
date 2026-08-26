using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// 権限グリッドの「状態」の選択肢 1 つ。
/// 表示名を持たせるだけの器で、値そのものはドメインの列挙型。
/// </summary>
/// <param name="Value">この選択肢が表す状態。</param>
public sealed record PermissionStateChoiceViewModel(PermissionState Value)
{
    /// <summary>選択肢の並び。どの行でも同じなので 1 組を使い回す。</summary>
    public static IReadOnlyList<PermissionStateChoiceViewModel> All { get; } =
        PermissionStates.All.Select(state => new PermissionStateChoiceViewModel(state)).ToList();

    public string DisplayName => Value.DisplayName();

    /// <summary>その状態を表す選択肢。並びに無い状態は「指定なし」に落とす。</summary>
    public static PermissionStateChoiceViewModel For(PermissionState state) =>
        All.FirstOrDefault(choice => choice.Value == state) ?? All[0];
}
