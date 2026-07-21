using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kostyor.Core.Equivalents;
using Kostyor.Core.Formatting;
using Kostyor.Core.History;
using Kostyor.Core.Money;

namespace Kostyor.App.ViewModels;

/// <summary>Карточка итога встречи (ТЗ §4, дизайн — модалка «Итог встречи»).</summary>
public partial class SummaryViewModel : ObservableObject
{
    private readonly MeetingRecord _record;
    private readonly Action<bool> _onWorthIt;

    public event Action? ResetRequested;
    public event Action? SavePngRequested;

    public string FinalRubText { get; }
    public string ConversionText { get; }
    public string DurationText { get; }
    public string PersonHoursText { get; }
    public string ParticipantsText { get; }
    public string BurnText { get; }

    public ObservableCollection<EquivalentItem> Equivalents { get; } = new();

    [ObservableProperty] private string _copyLabel = "Скопировать в чат";
    [ObservableProperty] private bool? _worthIt;

    public SummaryViewModel(MeetingRecord record, RateConverter converter, Action<bool> onWorthIt)
        : this(record, converter, onWorthIt, EquivalentPrices.Default) { }

    public SummaryViewModel(MeetingRecord record, RateConverter converter, Action<bool> onWorthIt, EquivalentPrices prices)
    {
        _record = record;
        _onWorthIt = onWorthIt;

        FinalRubText = RuFormat.Money(record.TotalRub);
        ConversionText = $"$ {RuFormat.Money(record.TotalUsd)}  ·  € {RuFormat.Money(record.TotalEur)}  ·  ¥ {RuFormat.Money(record.TotalCny)}";
        DurationText = RuFormat.Time(record.Duration);
        PersonHoursText = record.PersonHours.ToString("0.#", RuFormat.Ru);
        ParticipantsText = record.Headcount.ToString(CultureInfo.InvariantCulture);

        var ratePerHour = record.Duration.TotalHours > 0
            ? (double)record.TotalRub / record.Duration.TotalHours
            : 0d;
        BurnText = RuFormat.BurnPerMinute((decimal)(ratePerHour));

        foreach (var (icon, text) in EquivalentsCalc.SummaryEquivalents(record.TotalRub, prices))
            Equivalents.Add(MakePill(icon, text));
    }

    private static EquivalentItem MakePill(string icon, string text) => icon switch
    {
        "📱" => new EquivalentItem(icon, text, "#1F60A5FA", "#4060A5FA", "#93c5fd"),
        "💻" => new EquivalentItem(icon, text, "#1FF43F5E", "#40F43F5E", "#fda4af"),
        _ => new EquivalentItem(icon, text, "#1FFB923C", "#40FB923C", "#fdba74"),
    };

    public string BuildSummaryText()
    {
        var s = _record;
        return "Встреча «Костёр»\n"
             + $"Длительность: {RuFormat.Time(s.Duration)}\n"
             + $"Участников: {s.Headcount} ({s.PersonHours.ToString("0.#", RuFormat.Ru)} чел·ч)\n"
             + $"Сожжено: {RuFormat.Money(s.TotalRub)} ₽ · ${RuFormat.Money(s.TotalUsd)} · €{RuFormat.Money(s.TotalEur)} · ¥{RuFormat.Money(s.TotalCny)}"
             + (s.WorthIt is null ? "" : s.WorthIt.Value ? "\nОценка: 👍 оно того стоило" : "\nОценка: 👎 зря собрались");
    }

    [RelayCommand]
    private void Copy()
    {
        try
        {
            System.Windows.Clipboard.SetText(BuildSummaryText());
            CopyLabel = "Скопировано ✓";
        }
        catch
        {
            CopyLabel = "Не удалось скопировать";
        }
    }

    [RelayCommand]
    private void SavePng() => SavePngRequested?.Invoke();

    [RelayCommand]
    private void Reset() => ResetRequested?.Invoke();

    [RelayCommand]
    private void ThumbUp()
    {
        WorthIt = true;
        _record.WorthIt = true;
        _onWorthIt(true);
    }

    [RelayCommand]
    private void ThumbDown()
    {
        WorthIt = false;
        _record.WorthIt = false;
        _onWorthIt(false);
    }

    public string SuggestedFileName =>
        $"kostyor_{_record.StartedAt:yyyyMMdd_HHmm}_{RuFormat.Money(_record.TotalRub).Replace(" ", "").Replace(" ", "")}rub.png";

    public record EquivalentItem(string Icon, string Text, string Bg, string Border, string Fg);
}
