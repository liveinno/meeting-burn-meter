using Kostyor.Core.Money;

namespace Kostyor.Core.Config;

/// <summary>
/// Курс валют, заданный пользователем (₽ за 1 единицу). Единственный источник курса —
/// сеть не используется. Nominal фиксирован = 1: человек мыслит «1 $ = N ₽».
/// </summary>
public sealed class ManualRatesConfig
{
    public decimal UsdPerUnit { get; set; } = 90m;
    public decimal EurPerUnit { get; set; } = 98m;
    public decimal CnyPerUnit { get; set; } = 12.5m;

    public RatesSnapshot ToSnapshot(DateTimeOffset date) => new(
        date,
        new Dictionary<string, CurrencyRate>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new(UsdPerUnit, 1),
            ["EUR"] = new(EurPerUnit, 1),
            ["CNY"] = new(CnyPerUnit, 1),
        },
        RatesOrigin.Manual);
}

/// <summary>Глобальные хоткеи (ТЗ §5). Строки в формате «Ctrl+Alt+K».</summary>
public sealed class HotkeysConfig
{
    public string ToggleVisibility { get; set; } = "Ctrl+Alt+K";
    public string StartPause { get; set; } = "Ctrl+Alt+Space";
    public string CaptureMode { get; set; } = "Ctrl+Shift+C";
}

/// <summary>
/// Конфиг приложения — <c>%APPDATA%\Kostyor\config.json</c> (AGENTS §5, ТЗ §2).
/// Роли/ставки, overhead, пороги кольца, вехи, шаблоны, дефолтные курсы, хоткеи.
/// </summary>
public sealed class AppConfig
{
    public List<RolePreset> Roles { get; set; } = DefaultRoles();

    /// <summary>Начальный состав (как в дизайне: 2 разработчика, 1 аналитик, 1 тимлид).</summary>
    public Dictionary<string, int> InitialCounts { get; set; } = new()
    {
        ["dev"] = 2,
        ["analyst"] = 1,
        ["lead"] = 1,
    };

    /// <summary>Коэффициент полной нагрузки (ТЗ §1). 1.0 = оклад; 1.3–2.3 = стоимость для компании.</summary>
    public decimal Overhead { get; set; } = 1.0m;

    /// <summary>Пороги смены цвета кольца, минуты (ТЗ §3).</summary>
    public List<int> RingThresholdsMinutes { get; set; } = new() { 15, 30, 45 };

    /// <summary>За сколько минут кольцо заполняется полностью (prop дизайна <c>fillMinutes</c>).</summary>
    public int FillMinutes { get; set; } = 60;

    /// <summary>Пороги вех-нотификаций, ₽ (ТЗ §3, дефолт 1к/5к/10к/50к).</summary>
    public List<decimal> MilestonesRub { get; set; } = new() { 1000m, 5000m, 10000m, 50000m };

    /// <summary>Часов в месяце для помощника «оклад → ставка/час» (ТЗ §2, дефолт 160).</summary>
    public decimal SalaryHoursPerMonth { get; set; } = 160m;

    public bool ShowConversion { get; set; } = true;
    public bool PulseAtRed { get; set; } = true;
    public bool ShowEquivalents { get; set; } = true;

    /// <summary>Цена чашки кофе для живых эквивалентов, ₽ (ТЗ §3).</summary>
    public decimal CoffeePriceRub { get; set; } = 250m;

    /// <summary>Часовая ставка по МРОТ для эквивалентов, ₽ (≈ МРОТ 2025 22440 ₽ / 160 ч, см. README).</summary>
    public decimal MrotHourlyRub { get; set; } = 140m;

    /// <summary>Цена «одной пиццы на команду» для карточки итога, ₽ (настраиваемо; 0 — скрыть).</summary>
    public decimal PizzaPriceRub { get; set; } = 1000m;

    /// <summary>Цена «смартфона» для карточки итога, ₽ (настраиваемо; 0 — скрыть).</summary>
    public decimal SmartphonePriceRub { get; set; } = 5000m;

    /// <summary>Цена «ноутбука/MacBook» для карточки итога, ₽ (настраиваемо; 0 — скрыть).</summary>
    public decimal LaptopPriceRub { get; set; } = 10000m;

    /// <summary>Курс валют, заданный пользователем (₽ за единицу). Единственный источник курса — сеть не используется.</summary>
    public ManualRatesConfig ManualRates { get; set; } = new();

    /// <summary>Огонь в кольце (design v2): включён ли эффект.</summary>
    public bool FireEnabled { get; set; } = true;

    /// <summary>Порог суммы для жёлтой фазы огня, ₽ (0 — не зажигать).</summary>
    public decimal FireYellowRub { get; set; } = 1000m;

    /// <summary>Порог суммы для оранжевой фазы огня, ₽.</summary>
    public decimal FireOrangeRub { get; set; } = 2000m;

    /// <summary>Порог суммы для красной фазы огня, ₽.</summary>
    public decimal FireRedRub { get; set; } = 3000m;
    public HotkeysConfig Hotkeys { get; set; } = new();
    public List<MeetingTemplate> Templates { get; set; } = new();

    public bool StartMinimized { get; set; } = false;
    public bool Autostart { get; set; } = false;
    public bool ClickThrough { get; set; } = false;

    /// <summary>Масштаб круга/UI, задаётся пользователем растягиванием за обод (сохраняется между запусками).
    /// 0.6 — базовый (дизайн в 420-координатах). Границы применяются в окне.</summary>
    public double UiScale { get; set; } = 0.6;

    /// <summary>Обучающий тур уже показан (ТЗ часть B) — не показывать сам по себе повторно.</summary>
    public bool OnboardingDone { get; set; } = false;

    public static List<RolePreset> DefaultRoles() => new()
    {
        new("analyst", "Аналитик", 1000m),
        new("dev", "Разработчик", 1500m),
        new("senior", "Senior", 2500m),
        new("lead", "Тимлид", 3000m),
        new("qa", "QA", 900m),
        new("design", "Дизайнер", 1200m),
        new("manager", "Менеджер", 1800m),
        new("clevel", "C-level", 6000m),
    };
}
