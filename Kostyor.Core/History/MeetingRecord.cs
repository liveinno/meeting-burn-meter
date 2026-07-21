namespace Kostyor.Core.History;

/// <summary>
/// Запись истории встречи (ТЗ §4). Хранится в SQLite <c>%APPDATA%\Kostyor\history.db</c>.
/// Оценка «оно того стоило?» — <see cref="WorthIt"/> (true 👍 / false 👎 / null — без оценки).
/// </summary>
public sealed class MeetingRecord
{
    public long Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public double DurationSeconds { get; set; }
    public decimal TotalRub { get; set; }
    public decimal TotalUsd { get; set; }
    public decimal TotalEur { get; set; }
    public decimal TotalCny { get; set; }
    public int Headcount { get; set; }
    public double PersonHours { get; set; }

    /// <summary>Человекочитаемый состав, напр. «Разработчик×2, Тимлид×1».</summary>
    public string Composition { get; set; } = "";

    public bool? WorthIt { get; set; }

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
}
