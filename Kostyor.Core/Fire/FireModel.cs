namespace Kostyor.Core.Fire;

/// <summary>Прозрачности слоёв огня для фазы (1:1 из дизайна v2 «Костёр — огонь»).</summary>
public readonly record struct FireVisual(
    double EmberOpacity, double TongueOpacity, double BlazeOpacity, double HeatGlowOpacity, double ScrimOpacity);

/// <summary>
/// Огонь в кольце (design v2): фаза 0..3 (нет огня / жёлтый / оранжевый / красный) и
/// прозрачности слоёв. Чистые функции — тестируемо. Порог ≤ 0 считается «не задан» и не зажигает фазу.
/// </summary>
public static class FireModel
{
    /// <summary>Фаза по сожжённой сумме: 0..3. Сравнение сверху вниз (красный → оранжевый → жёлтый).</summary>
    public static int MoneyPhase(decimal rub, decimal yellowRub, decimal orangeRub, decimal redRub)
    {
        if (redRub > 0m && rub >= redRub) return 3;
        if (orangeRub > 0m && rub >= orangeRub) return 2;
        if (yellowRub > 0m && rub >= yellowRub) return 1;
        return 0;
    }

    /// <summary>Прозрачности слоёв огня по фазе (значения из дизайна).</summary>
    public static FireVisual Visual(int phase) => phase switch
    {
        >= 3 => new FireVisual(1.0, 1.0, 1.0, 0.95, 0.55),
        2    => new FireVisual(1.0, 1.0, 0.0, 0.70, 0.30),
        1    => new FireVisual(0.75, 0.0, 0.0, 0.45, 0.0),
        _    => new FireVisual(0.0, 0.0, 0.0, 0.0, 0.0),
    };
}
