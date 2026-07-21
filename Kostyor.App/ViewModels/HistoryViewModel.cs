using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kostyor.Core.Formatting;
using Kostyor.Core.History;
using Kostyor.App.Services;

namespace Kostyor.App.ViewModels;

/// <summary>Экран истории (ТЗ §4): сумма за неделю/месяц, доля 👎-встреч, экспорт CSV.</summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryRepository _history;
    private readonly Logger _log;

    public ObservableCollection<HistoryRow> Rows { get; } = new();

    [ObservableProperty] private string _weekTotalText = "";
    [ObservableProperty] private string _monthTotalText = "";
    [ObservableProperty] private string _thumbsDownText = "";
    [ObservableProperty] private string _emptyHintText = "";

    /// <summary>Колбэк выбора пути CSV (окно показывает диалог); null — отмена.</summary>
    public Func<string?>? RequestSavePath { get; set; }

    public HistoryViewModel(HistoryRepository history, Logger log)
    {
        _history = history;
        _log = log;
        Load();
    }

    private void Load()
    {
        Rows.Clear();
        var all = _history.All();
        foreach (var r in all)
            Rows.Add(new HistoryRow(r));

        var now = DateTimeOffset.Now;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddMonths(-1);

        var week = all.Where(r => r.EndedAt >= weekAgo).Sum(r => r.TotalRub);
        var month = all.Where(r => r.EndedAt >= monthAgo).Sum(r => r.TotalRub);
        WeekTotalText = $"{RuFormat.Money(week)} ₽";
        MonthTotalText = $"{RuFormat.Money(month)} ₽";

        var rated = all.Where(r => r.WorthIt is not null).ToList();
        if (rated.Count > 0)
        {
            var bad = rated.Count(r => r.WorthIt == false);
            var share = 100.0 * bad / rated.Count;
            ThumbsDownText = $"{share.ToString("0", CultureInfo.InvariantCulture)}% встреч оценены как 👎 ({bad} из {rated.Count})";
        }
        else
        {
            ThumbsDownText = "Оценок пока нет";
        }

        EmptyHintText = all.Count == 0 ? "История пуста — проведите первую встречу." : "";
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var path = RequestSavePath?.Invoke();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("StartedAt;EndedAt;DurationSec;Rub;Usd;Eur;Cny;Headcount;PersonHours;Composition;WorthIt");
            foreach (var r in _history.All())
            {
                var inv = CultureInfo.InvariantCulture;
                sb.AppendLine(string.Join(';',
                    r.StartedAt.ToString("o"),
                    r.EndedAt.ToString("o"),
                    r.DurationSeconds.ToString(inv),
                    r.TotalRub.ToString(inv),
                    r.TotalUsd.ToString(inv),
                    r.TotalEur.ToString(inv),
                    r.TotalCny.ToString(inv),
                    r.Headcount.ToString(inv),
                    r.PersonHours.ToString(inv),
                    '"' + r.Composition.Replace("\"", "\"\"") + '"',
                    r.WorthIt is null ? "" : (r.WorthIt.Value ? "1" : "0")));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            _log.Info($"История экспортирована в CSV: {path}");
        }
        catch (Exception ex)
        {
            _log.Error("Не удалось экспортировать CSV", ex);
        }
    }

    public sealed class HistoryRow
    {
        public HistoryRow(MeetingRecord r)
        {
            DateText = r.StartedAt.ToString("dd.MM.yyyy HH:mm", RuFormat.Ru);
            DurationText = RuFormat.Time(r.Duration);
            RubText = $"{RuFormat.Money(r.TotalRub)} ₽";
            Composition = r.Composition;
            HeadcountText = r.Headcount.ToString(CultureInfo.InvariantCulture);
            WorthText = r.WorthIt is null ? "—" : (r.WorthIt.Value ? "👍" : "👎");
        }

        public string DateText { get; }
        public string DurationText { get; }
        public string RubText { get; }
        public string Composition { get; }
        public string HeadcountText { get; }
        public string WorthText { get; }
    }
}
