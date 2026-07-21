using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kostyor.Core.Config;
using Kostyor.Core.Formatting;
using Kostyor.Core.Roster;
using Kostyor.App.Services;

namespace Kostyor.App.ViewModels;

/// <summary>Настройки (ТЗ §2/§3/§5): overhead, пороги, вехи, помощник «оклад→ставка», хоткеи, автозапуск.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly Logger _log;
    private readonly string _exePath;
    private readonly DispatcherTimer _autoSave;

    public event Action? Saved;

    [ObservableProperty] private double _overhead;
    [ObservableProperty] private int _fillMinutes;
    [ObservableProperty] private int _threshold1;
    [ObservableProperty] private int _threshold2;
    [ObservableProperty] private int _threshold3;
    [ObservableProperty] private string _milestonesText = "";
    [ObservableProperty] private double _salaryHoursPerMonth;
    [ObservableProperty] private double _coffeePrice;
    [ObservableProperty] private double _mrotHourly;
    [ObservableProperty] private double _pizzaPrice;
    [ObservableProperty] private double _smartphonePrice;
    [ObservableProperty] private double _laptopPrice;
    [ObservableProperty] private double _manualUsd;
    [ObservableProperty] private double _manualEur;
    [ObservableProperty] private double _manualCny;
    [ObservableProperty] private bool _fireEnabled;
    [ObservableProperty] private double _fireYellowRub;
    [ObservableProperty] private double _fireOrangeRub;
    [ObservableProperty] private double _fireRedRub;
    [ObservableProperty] private bool _showConversion;
    [ObservableProperty] private bool _pulseAtRed;
    [ObservableProperty] private bool _showEquivalents;
    [ObservableProperty] private bool _autostart;
    [ObservableProperty] private bool _clickThrough;
    [ObservableProperty] private bool _startMinimized;

    // Помощник «оклад/мес → ставка/час».
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SalaryResultText))]
    private string _salaryInput = "";

    public string HotkeysText =>
        $"Показать/скрыть: {_config.Hotkeys.ToggleVisibility}\n" +
        $"Старт/пауза: {_config.Hotkeys.StartPause}\n" +
        $"Режим захвата: {_config.Hotkeys.CaptureMode}";

    public SettingsViewModel(AppConfig config, Logger log, string exePath)
    {
        _config = config;
        _log = log;
        _exePath = exePath;

        _overhead = (double)config.Overhead;
        _fillMinutes = config.FillMinutes;
        _threshold1 = config.RingThresholdsMinutes.ElementAtOrDefault(0);
        _threshold2 = config.RingThresholdsMinutes.ElementAtOrDefault(1);
        _threshold3 = config.RingThresholdsMinutes.ElementAtOrDefault(2);
        _milestonesText = string.Join(", ", config.MilestonesRub.Select(m => RuFormat.Number(m)));
        _salaryHoursPerMonth = (double)config.SalaryHoursPerMonth;
        _coffeePrice = (double)config.CoffeePriceRub;
        _mrotHourly = (double)config.MrotHourlyRub;
        _pizzaPrice = (double)config.PizzaPriceRub;
        _smartphonePrice = (double)config.SmartphonePriceRub;
        _laptopPrice = (double)config.LaptopPriceRub;
        _showConversion = config.ShowConversion;
        _pulseAtRed = config.PulseAtRed;
        _showEquivalents = config.ShowEquivalents;
        _autostart = StartupRegistration.IsEnabled();
        _clickThrough = config.ClickThrough;
        _startMinimized = config.StartMinimized;

        // Курс валют — локальный, из настроек (сеть не используется).
        _manualUsd = (double)config.ManualRates.UsdPerUnit;
        _manualEur = (double)config.ManualRates.EurPerUnit;
        _manualCny = (double)config.ManualRates.CnyPerUnit;

        _fireEnabled = config.FireEnabled;
        _fireYellowRub = (double)config.FireYellowRub;
        _fireOrangeRub = (double)config.FireOrangeRub;
        _fireRedRub = (double)config.FireRedRub;

        // Автосохранение: любое изменение настройки → сохранить и применить (с дебаунсом ~350 мс,
        // чтобы драг слайдера/ввод в поле не писали конфиг на каждый тик).
        _autoSave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _autoSave.Tick += (_, _) => { _autoSave.Stop(); Save(); };
        PropertyChanged += OnSettingChanged;
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Поле помощника «оклад→ставка» не сохраняется — это калькулятор, не настройка.
        if (e.PropertyName is nameof(SalaryInput) or nameof(SalaryResultText)) return;
        _autoSave.Stop();
        _autoSave.Start();
    }

    public string SalaryResultText
    {
        get
        {
            if (!decimal.TryParse(SalaryInput.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var salary) || salary <= 0)
                return "—";
            var hourly = RosterMath.SalaryToHourly(salary, (decimal)Math.Max(1, SalaryHoursPerMonth));
            return $"{RuFormat.Number(Math.Round(hourly))} ₽/час";
        }
    }

    [RelayCommand]
    private void Save()
    {
        _config.Overhead = (decimal)Math.Max(0, Overhead);
        _config.FillMinutes = Math.Clamp(FillMinutes, 5, 240);
        _config.RingThresholdsMinutes = new List<int> { Threshold1, Threshold2, Threshold3 };
        _config.MilestonesRub = ParseMilestones(MilestonesText);
        _config.SalaryHoursPerMonth = (decimal)Math.Max(1, SalaryHoursPerMonth);
        _config.CoffeePriceRub = (decimal)Math.Max(0, CoffeePrice);
        _config.MrotHourlyRub = (decimal)Math.Max(0, MrotHourly);
        _config.PizzaPriceRub = (decimal)Math.Max(0, PizzaPrice);
        _config.SmartphonePriceRub = (decimal)Math.Max(0, SmartphonePrice);
        _config.LaptopPriceRub = (decimal)Math.Max(0, LaptopPrice);
        _config.ManualRates.UsdPerUnit = (decimal)Math.Max(0, ManualUsd);
        _config.ManualRates.EurPerUnit = (decimal)Math.Max(0, ManualEur);
        _config.ManualRates.CnyPerUnit = (decimal)Math.Max(0, ManualCny);
        _config.FireEnabled = FireEnabled;
        _config.FireYellowRub = (decimal)Math.Max(0, FireYellowRub);
        _config.FireOrangeRub = (decimal)Math.Max(0, FireOrangeRub);
        _config.FireRedRub = (decimal)Math.Max(0, FireRedRub);
        _config.ShowConversion = ShowConversion;
        _config.PulseAtRed = PulseAtRed;
        _config.ShowEquivalents = ShowEquivalents;
        _config.ClickThrough = ClickThrough;
        _config.StartMinimized = StartMinimized;

        StartupRegistration.Set(Autostart, _exePath, _log);

        try
        {
            ConfigStore.Save(AppPaths.ConfigFile, _config);
            _log.Info("Настройки сохранены");
        }
        catch (Exception ex)
        {
            _log.Error("Не удалось сохранить настройки", ex);
        }

        Saved?.Invoke();
    }

    private static List<decimal> ParseMilestones(string text)
    {
        var list = new List<decimal>();
        foreach (var part in text.Split(new[] { ',', ';', ' ', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = part.Replace(" ", "").Replace(" ", "");
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0)
                list.Add(v);
        }
        return list.Count > 0 ? list.Distinct().OrderBy(x => x).ToList() : new List<decimal> { 1000, 5000, 10000, 50000 };
    }
}
