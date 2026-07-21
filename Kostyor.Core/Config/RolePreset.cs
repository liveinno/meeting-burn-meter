namespace Kostyor.Core.Config;

/// <summary>Пресет роли: латинский <see cref="Id"/>, русское имя и ставка ₽/час (ТЗ §2).</summary>
public sealed class RolePreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal RatePerHour { get; set; }

    public RolePreset() { }

    public RolePreset(string id, string name, decimal ratePerHour)
    {
        Id = id;
        Name = name;
        RatePerHour = ratePerHour;
    }
}

/// <summary>Шаблон встречи: сохранённый состав «роль → количество» (ТЗ §2).</summary>
public sealed class MeetingTemplate
{
    public string Name { get; set; } = "";
    public Dictionary<string, int> Counts { get; set; } = new();
}
