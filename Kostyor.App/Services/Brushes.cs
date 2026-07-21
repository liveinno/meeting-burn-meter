using System.Windows.Media;

namespace Kostyor.App.Services;

/// <summary>Кеш замороженных кистей для цветов кольца (hex из дизайна → Brush).</summary>
public static class BrushCache
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();

    public static SolidColorBrush FromHex(string hex)
    {
        if (Cache.TryGetValue(hex, out var b)) return b;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        Cache[hex] = brush;
        return brush;
    }
}
