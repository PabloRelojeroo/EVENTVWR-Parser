using System.Windows;
using System.Windows.Media;

namespace EventvwrParser.Models;

public sealed class CategoryItem {
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public Func<LogEvent, bool> Match { get; init; } = _ => true;
    public int Count { get; set; }
    public Thickness Indent { get; init; }
    public FontWeight Weight { get; init; } = FontWeights.Normal;
    public Brush Brush { get; init; } = Brushes.White;
}
