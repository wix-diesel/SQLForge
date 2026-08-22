using SQLForge.Domain.Connections;

namespace SQLForge.Ui.ViewModels;

/// <summary>環境タグの色見本 1 つ。危険度に応じた色分けは XAML 側の Classes で行う。</summary>
public sealed class EnvironmentChoiceViewModel(EnvironmentTag tag)
{
    public EnvironmentTag Tag { get; } = tag;

    public string DisplayName => Tag.DisplayName;

    public bool IsCritical => Tag.Severity == EnvironmentSeverity.Critical;

    public bool IsElevated => Tag.Severity == EnvironmentSeverity.Elevated;

    public bool IsNormal => Tag.Severity == EnvironmentSeverity.Normal;

    public bool IsNeutral => Tag.Severity == EnvironmentSeverity.Neutral;

    public static IReadOnlyList<EnvironmentChoiceViewModel> All { get; } =
        EnvironmentTag.All.Select(tag => new EnvironmentChoiceViewModel(tag)).ToList();

    public static EnvironmentChoiceViewModel For(EnvironmentTag tag) =>
        All.First(choice => choice.Tag.Equals(tag));
}
