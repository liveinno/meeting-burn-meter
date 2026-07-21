using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kostyor.Core;
using Kostyor.Core.Config;
using Kostyor.Core.Equivalents;
using Kostyor.Core.Fire;
using Kostyor.Core.Formatting;
using Kostyor.Core.History;
using Kostyor.Core.Money;
using Kostyor.Core.Ring;
using Kostyor.Core.Roster;
using Kostyor.Core.Session;
using Kostyor.App.Services;

namespace Kostyor.App.ViewModels;

/// <summary>
/// Главная ViewModel (MVVM, AGENTS §4). Держит <see cref="BurnEngine"/>, состав, курсы,
/// вехи и рендер-таймер. Деньги/время считаются от часов в движке; здесь — только отрисовка
/// значений раз в ~100 мс (ТЗ §1 «одометр — таймер рендера»).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly BurnEngine _engine = new();
    private readonly AppConfig _config;
    private readonly RatesService _rates;
    private readonly SessionStore _session;
    private readonly HistoryRepository _history;
    private readonly Logger _log;
    private readonly DispatcherTimer _render;
    private readonly Dispatcher _dispatcher;

    private RateConverter _converter;
    private readonly List<(decimal Threshold, string Icon, string Text)> _milestones;

    /// <summary>Цвета фаз кольца/огня (индекс = фаза 0..3), 1:1 с дизайном.</summary>
    private static readonly string[] PhaseHex = { RingModel.Green, RingModel.Yellow, RingModel.Orange, RingModel.Red };
    private readonly HashSet<decimal> _fired = new();
    private decimal _prevRub;
    private DateTimeOffset _startedAt;
    private long _lastHistoryId;

    /// <summary>Встреча остановлена — показать карточку итога (App подписывается).</summary>
    public event Action<SummaryViewModel>? MeetingStopped;

    public ObservableCollection<RoleRowViewModel> Roles { get; } = new();
    public ObservableCollection<ToastViewModel> Toasts { get; } = new();

    #region Наблюдаемые свойства (обновляются в рендер-тике / командами)

    [ObservableProperty] private string _timeText = "00:00";
    [ObservableProperty] private string _amountText = "0";
    [ObservableProperty] private string _conversionText = "";
    [ObservableProperty] private string _burnText = "";
    [ObservableProperty] private string _compactRubText = "0 ₽";
    [ObservableProperty] private string _headcountText = "";
    [ObservableProperty] private string _runningLineText = "";
    [ObservableProperty] private Brush _ringBrush = BrushCache.FromHex(RingModel.Green);
    [ObservableProperty] private Geometry _progressGeometry = Geometry.Empty;
    [ObservableProperty] private bool _isRed;

    // Огонь в кольце (design v2): фаза 0..3 (нет/жёлтый/оранжевый/красный) и включён ли эффект.
    [ObservableProperty] private int _firePhase;
    [ObservableProperty] private bool _fireActive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotRunning))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotCompact), nameof(ShowPanel))]
    private bool _isCompact;

    [ObservableProperty] private bool _showStop;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPanel))]
    private bool _panelOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotCustomForm))]
    private bool _customFormOpen;

    [ObservableProperty] private string _customName = "";
    [ObservableProperty] private string _customRate = "";
    [ObservableProperty] private bool _showConversion = true;
    [ObservableProperty] private bool _showEquivalents = true;

    public bool NotRunning => !IsRunning;
    public bool NotCompact => !IsCompact;
    public bool NotCustomForm => !CustomFormOpen;
    public bool ShowPanel => PanelOpen && !IsCompact;

    #endregion

    public MainViewModel(AppConfig config, RatesService rates, SessionStore session,
        HistoryRepository history, Logger log)
    {
        _config = config;
        _rates = rates;
        _session = session;
        _history = history;
        _log = log;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _converter = new RateConverter(_rates.Current);
        ShowConversion = config.ShowConversion;
        ShowEquivalents = config.ShowEquivalents;
        _startedAt = DateTimeOffset.Now;

        _milestones = BuildMilestones(config);
        BuildRoles();
        RecomputeRate();

        _render = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _render.Tick += (_, _) => UpdateVisuals();
        _render.Start();
        UpdateVisuals();
    }

    /// <summary>Пересобрать конвертер после обновления курсов ЦБ.</summary>
    public void OnRatesUpdated() => _converter = new RateConverter(_rates.Current);

    /// <summary>Применить изменения настроек на лету (после автосохранения из окна настроек).</summary>
    public void ApplyConfigChanges()
    {
        ShowConversion = _config.ShowConversion;
        ShowEquivalents = _config.ShowEquivalents;
        _milestones.Clear();
        _milestones.AddRange(BuildMilestones(_config));
        RecomputeRate();   // overhead мог измениться
        UpdateVisuals();   // пороги/заполнение/эквиваленты читаются из конфига здесь же
    }

    #region Состав

    private void BuildRoles()
    {
        Roles.Clear();
        foreach (var preset in _config.Roles)
        {
            var count = _config.InitialCounts.TryGetValue(preset.Id, out var c) ? c : 0;
            AddRoleRow(preset.Id, preset.Name, preset.RatePerHour, count, isPreset: true);
        }
    }

    private RoleRowViewModel AddRoleRow(string id, string name, decimal rate, int count, bool isPreset)
    {
        var row = new RoleRowViewModel(id, name, rate, count, isPreset);
        row.Changed += OnRoleChanged;
        Roles.Add(row);
        return row;
    }

    private void OnRoleChanged()
    {
        RecomputeRate();
        PersistPresetEdits();
    }

    private void RecomputeRate()
    {
        var lines = Roles.Select(r => new RoleRate(r.RatePerHour, r.Count));
        var ratePerHour = RosterMath.RatePerHour(lines, _config.Overhead);
        _engine.SetRatePerHour(ratePerHour); // если идёт — flush, ставка «с этого момента» (интегрально)
        UpdateVisuals();
    }

    private void PersistPresetEdits()
    {
        try
        {
            foreach (var row in Roles.Where(r => r.IsPreset))
            {
                var preset = _config.Roles.FirstOrDefault(p => p.Id == row.Id);
                if (preset is null) continue;
                preset.Name = row.Name;
                preset.RatePerHour = row.RatePerHour;
            }
            foreach (var row in Roles)
                _config.InitialCounts[row.Id] = row.Count;
            ConfigStore.Save(AppPaths.ConfigFile, _config);
        }
        catch (Exception ex)
        {
            _log.Error("Не удалось сохранить правки ролей в конфиг", ex);
        }
    }

    #endregion

    #region Команды управления

    [RelayCommand]
    private void ToggleRun()
    {
        if (_engine.IsRunning)
        {
            _engine.Pause();
            IsRunning = false;
        }
        else
        {
            if (_engine.Elapsed == TimeSpan.Zero) _startedAt = DateTimeOffset.Now;
            _engine.Start();
            IsRunning = true;
        }
        UpdateVisuals();
    }

    [RelayCommand]
    private void Stop()
    {
        if (_engine.Elapsed == TimeSpan.Zero) return;
        _engine.Pause();

        var summary = BuildSummaryAndSave();   // снимок итога строится до сброса
        _session.Clear();
        MeetingStopped?.Invoke(summary);

        // Встреча завершена — обнуляем круг (таймер/сумма → 0). Карточка итога уже готова по снимку.
        ResetMeeting();
    }

    /// <summary>«Новая встреча»: полный сброс.</summary>
    public void ResetMeeting()
    {
        _engine.Reset();
        IsRunning = false;
        IsCompact = false;
        PanelOpen = false;
        _fired.Clear();
        _prevRub = 0m;
        Toasts.Clear();
        _startedAt = DateTimeOffset.Now;
        _lastHistoryId = 0;
        _session.Clear();
        // _engine.Reset() обнулил ставку — вернуть её из текущего состава, иначе после «Новой встречи»
        // (и после «Стоп») секундомер идёт, а деньги стоят на 0. RecomputeRate сам вызовет UpdateVisuals.
        RecomputeRate();
    }

    [RelayCommand]
    private void TogglePanel() => PanelOpen = !PanelOpen;

    [RelayCommand]
    private void ToggleCompact() => IsCompact = !IsCompact;

    [RelayCommand]
    private void ToggleCustomForm() => CustomFormOpen = !CustomFormOpen;

    [RelayCommand]
    private void AddCustom()
    {
        var name = CustomName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (!int.TryParse(CustomRate.Trim(), out var rate) || rate <= 0) return;

        var id = "c_" + Guid.NewGuid().ToString("N")[..8];
        AddRoleRow(id, name, rate, count: 1, isPreset: false);
        CustomName = "";
        CustomRate = "";
        CustomFormOpen = false;
        RecomputeRate();
    }

    #endregion

    #region Рендер

    private void UpdateVisuals()
    {
        var rub = _engine.CurrentRub;
        var elapsed = _engine.Elapsed;
        var ring = RingModel.Compute(elapsed, _config.RingThresholdsMinutes, _config.FillMinutes);

        TimeText = RuFormat.Time(elapsed);
        AmountText = RuFormat.Money(rub);
        CompactRubText = RuFormat.Money(rub) + " ₽";
        BurnText = RuFormat.BurnPerMinute(_engine.RatePerHour);
        ConversionText = BuildConversion(rub);
        RunningLineText = _config.ShowEquivalents
            ? EquivalentsCalc.RunningLine(rub, _config.CoffeePriceRub, _config.MrotHourlyRub)
            : "";

        // Фаза огня (design v2): максимум из фазы по времени (цвет кольца) и по деньгам.
        // Цвет кольца и огонь берут один и тот же phase — как в макете.
        var timePhase = Array.IndexOf(PhaseHex, ring.Color);
        if (timePhase < 0) timePhase = 0;
        var moneyPhase = FireModel.MoneyPhase(rub, _config.FireYellowRub, _config.FireOrangeRub, _config.FireRedRub);
        var phase = _config.FireEnabled ? Math.Max(timePhase, moneyPhase) : timePhase;

        RingBrush = BrushCache.FromHex(PhaseHex[phase]);
        ProgressGeometry = ArcBuilder.Build(ring.Progress);
        IsRed = phase == 3 && _config.PulseAtRed;
        FirePhase = phase;
        FireActive = _config.FireEnabled;

        var headcount = Roles.Sum(r => r.Count);
        HeadcountText = RuFormat.HeadcountLine(headcount, _engine.RatePerHour);
        ShowStop = elapsed > TimeSpan.Zero;
        IsRunning = _engine.IsRunning;

        CheckMilestones(rub);
        _prevRub = rub;
    }

    private string BuildConversion(decimal rub)
    {
        string Part(string sym, string code)
        {
            var v = _converter.Convert(rub, code);
            return v is null ? $"{sym} —" : $"{sym} {RuFormat.Money(v.Value)}";
        }
        return $"{Part("$", "USD")}  ·  {Part("€", "EUR")}  ·  {Part("¥", "CNY")}";
    }

    #endregion

    #region Вехи

    private static List<(decimal, string, string)> BuildMilestones(AppConfig config)
    {
        var list = new List<(decimal, string, string)>();
        foreach (var th in config.MilestonesRub.Distinct().OrderBy(x => x))
        {
            var known = EquivalentsCalc.DefaultMilestones.FirstOrDefault(m => m.ThresholdRub == th);
            if (known.ThresholdRub == th)
                list.Add((th, known.Icon, known.Text));
            else
                list.Add((th, "🔥", $"Сожжено {RuFormat.Money(th)} ₽"));
        }
        return list;
    }

    private void CheckMilestones(decimal rub)
    {
        foreach (var (th, icon, text) in _milestones)
        {
            if (rub >= th && _prevRub < th && _fired.Add(th))
                ShowToast(icon, text);
        }
    }

    private void ShowToast(string icon, string text)
    {
        var id = Guid.NewGuid().ToString("N");
        var toast = new ToastViewModel(id, icon, text);
        Toasts.Add(toast);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var found = Toasts.FirstOrDefault(t => t.Id == id);
            if (found is not null) Toasts.Remove(found);
        };
        timer.Start();
    }

    #endregion

    #region Итог / история

    private SummaryViewModel BuildSummaryAndSave()
    {
        var rub = _engine.CurrentRub;
        var elapsed = _engine.Elapsed;
        var headcount = Roles.Sum(r => r.Count);
        var personHours = headcount * elapsed.TotalHours;

        decimal Conv(string code) => _converter.Convert(rub, code) ?? 0m;

        var record = new MeetingRecord
        {
            StartedAt = _startedAt,
            EndedAt = DateTimeOffset.Now,
            DurationSeconds = elapsed.TotalSeconds,
            TotalRub = rub,
            TotalUsd = Conv("USD"),
            TotalEur = Conv("EUR"),
            TotalCny = Conv("CNY"),
            Headcount = headcount,
            PersonHours = personHours,
            Composition = BuildComposition(),
            WorthIt = null,
        };
        var historyId = _history.Add(record);
        _lastHistoryId = historyId;
        _log.Info($"Встреча завершена: {RuFormat.Money(rub)} ₽, {RuFormat.Time(elapsed)}, {headcount} чел.");

        var prices = new EquivalentPrices(_config.PizzaPriceRub, _config.SmartphonePriceRub, _config.LaptopPriceRub);
        // Оценку сохраняем по локально захваченному id — сброс встречи после стопа его не затрёт.
        return new SummaryViewModel(record, _converter,
            worthIt => { if (historyId > 0) _history.SetWorthIt(historyId, worthIt); }, prices);
    }

    private string BuildComposition()
    {
        var sb = new StringBuilder();
        foreach (var r in Roles.Where(r => r.Count > 0))
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(r.Name).Append('×').Append(r.Count);
        }
        return sb.Length == 0 ? "—" : sb.ToString();
    }

    #endregion

    #region Сессия (автосохранение/восстановление)

    public bool IsMeetingInProgress => _engine.Elapsed > TimeSpan.Zero;

    public SessionSnapshot CaptureSession()
    {
        var s = _engine.Capture();
        var snap = new SessionSnapshot
        {
            SavedAt = DateTimeOffset.Now,
            StartedAt = _startedAt,
            AccumulatedRub = s.AccumulatedRub,
            AccumulatedSeconds = s.AccumulatedSeconds,
            RatePerSecond = s.RatePerSecond,
            Running = s.Running,
            FiredMilestones = _fired.ToList(),
        };
        foreach (var r in Roles)
        {
            snap.Counts[r.Id] = r.Count;
            if (r.IsPreset)
            {
                var preset = _config.Roles.FirstOrDefault(p => p.Id == r.Id);
                if (preset is not null && (preset.Name != r.Name || preset.RatePerHour != r.RatePerHour))
                {
                    snap.NameOverrides[r.Id] = r.Name;
                    snap.RateOverrides[r.Id] = r.RatePerHour;
                }
            }
            else
            {
                snap.Customs.Add(new SessionSnapshot.CustomRole { Id = r.Id, Name = r.Name, RatePerHour = r.RatePerHour });
            }
        }
        return snap;
    }

    public void RestoreSession(SessionSnapshot snap)
    {
        _startedAt = snap.StartedAt;
        _fired.Clear();
        foreach (var m in snap.FiredMilestones) _fired.Add(m);

        // Кастомные роли.
        foreach (var c in snap.Customs)
            if (Roles.All(r => r.Id != c.Id))
                AddRoleRow(c.Id, c.Name, c.RatePerHour, count: 0, isPreset: false);

        // Правки имён/ставок пресетов.
        foreach (var row in Roles)
        {
            if (snap.NameOverrides.TryGetValue(row.Id, out var nm)) row.Name = nm;
            if (snap.RateOverrides.TryGetValue(row.Id, out var rt)) row.RatePerHour = rt;
            if (snap.Counts.TryGetValue(row.Id, out var cnt)) row.Count = cnt;
        }

        _engine.Restore(new BurnState(snap.AccumulatedRub, snap.AccumulatedSeconds, snap.RatePerSecond, snap.Running));
        // Ставку берём из восстановленного состава, чтобы она была консистентна с ростером.
        RecomputeRate();
        IsRunning = _engine.IsRunning;
        _prevRub = _engine.CurrentRub;
        UpdateVisuals();
    }

    public void AutoSave()
    {
        if (IsMeetingInProgress) _session.Save(CaptureSession());
    }

    #endregion

    public void StopRenderTimer() => _render.Stop();
}
