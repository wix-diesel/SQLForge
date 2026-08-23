using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// ツリーの深さを字下げ幅に変える。<see cref="Avalonia.Controls.TreeViewItem.Level"/> を
/// そのまま余白に写すだけで、Fluent 側の既定値には依存しない
/// （モックアップの 13px 刻みに合わせている）。
/// </summary>
public sealed class TreeIndentConverter : IValueConverter
{
    /// <summary>1 段ぶんの字下げ幅。</summary>
    public double Step { get; init; } = 13;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness(value is int level && level > 0 ? level * Step : 0, 0, 0, 0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
