namespace Kostyor.Core.Ring;

/// <summary>Визуальное состояние кольца-прогресса: цвет (hex), заполнение 0..1, красная зона.</summary>
public readonly record struct RingVisual(string Color, double Progress, bool IsRed);

/// <summary>
/// Логика кольца-прогресса (1:1 с дизайном <c>Костёр.dc.html</c>):
/// зелёный → жёлтый → оранжевый → красный по порогам минут (по умолчанию 15/30/45),
/// заполнение по окружности за <c>fillMinutes</c> (по умолчанию 60, prop дизайна).
/// Цвета — те же hex, что в макете. Чистая функция, покрыта xUnit.
/// </summary>
public static class RingModel
{
    public const string Green = "#34d399";
    public const string Yellow = "#fbbf24";
    public const string Orange = "#fb923c";
    public const string Red = "#f43f5e";

    public static readonly IReadOnlyList<int> DefaultThresholdsMinutes = new[] { 15, 30, 45 };
    public const int DefaultFillMinutes = 60;

    public static RingVisual Compute(TimeSpan elapsed, IReadOnlyList<int>? thresholdsMinutes = null, int fillMinutes = DefaultFillMinutes)
    {
        var t = thresholdsMinutes is { Count: >= 3 } ? thresholdsMinutes : DefaultThresholdsMinutes;
        var minutes = Math.Max(0d, elapsed.TotalMinutes);

        var color = Green;
        var isRed = false;
        if (minutes >= t[2]) { color = Red; isRed = true; }
        else if (minutes >= t[1]) color = Orange;
        else if (minutes >= t[0]) color = Yellow;

        var fillSeconds = fillMinutes <= 0 ? 0d : fillMinutes * 60d;
        var progress = fillSeconds <= 0d
            ? 1d
            : Math.Clamp(Math.Max(0d, elapsed.TotalSeconds) / fillSeconds, 0d, 1d);

        return new RingVisual(color, progress, isRed);
    }
}
