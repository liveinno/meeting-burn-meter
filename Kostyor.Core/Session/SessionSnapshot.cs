namespace Kostyor.Core.Session;

/// <summary>
/// Снимок сессии для восстановления после падения/перезапуска (ТЗ §5, AGENTS §4).
/// Пишется на диск раз в ~5 с — не только при закрытии (при падении обработчик закрытия не вызовется).
/// Содержит состояние счётчика + состав, чтобы восстановить встречу «как была».
/// </summary>
public sealed class SessionSnapshot
{
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }

    // Состояние BurnEngine (см. BurnState).
    public decimal AccumulatedRub { get; set; }
    public double AccumulatedSeconds { get; set; }
    public decimal RatePerSecond { get; set; }
    public bool Running { get; set; }

    // Состав и правки пользователя.
    public Dictionary<string, int> Counts { get; set; } = new();
    public Dictionary<string, decimal> RateOverrides { get; set; } = new();
    public Dictionary<string, string> NameOverrides { get; set; } = new();
    public List<CustomRole> Customs { get; set; } = new();

    // Уже показанные вехи — чтобы не сыпать тостами повторно после восстановления.
    public List<decimal> FiredMilestones { get; set; } = new();

    public sealed class CustomRole
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal RatePerHour { get; set; }
    }
}
