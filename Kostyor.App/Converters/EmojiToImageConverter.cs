using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Kostyor.App.Converters;

/// <summary>
/// Эмодзи-строка → цветная векторная иконка (ресурсы Emoji.xaml). WPF не рисует цветные
/// COLR-эмодзи, поэтому ключевые заменяем своими <see cref="DrawingImage"/>. Нет соответствия — null.
/// </summary>
public sealed class EmojiToImageConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["🍕"] = "Emoji.Pizza",
        ["📱"] = "Emoji.Phone",
        ["💻"] = "Emoji.Laptop",
        ["🔥"] = "Emoji.Fire",
        ["👍"] = "Emoji.ThumbUp",
        ["👎"] = "Emoji.ThumbDown",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrEmpty(s)) return null;
        if (Map.TryGetValue(s.Trim(), out var key)
            && Application.Current?.TryFindResource(key) is ImageSource img)
            return img;
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
