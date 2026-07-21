using System.Text.Json;

namespace Kostyor.Core.Config;

/// <summary>
/// Загрузка/сохранение <see cref="AppConfig"/> в JSON. Битый/отсутствующий конфиг →
/// дефолты (не падать — ТЗ «Качество и логи»). Чистое файловое IO, тестируется на temp-файлах.
/// </summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Читает конфиг; при любой ошибке возвращает дефолтный <see cref="AppConfig"/>.</summary>
    public static AppConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options);
                if (cfg is not null)
                {
                    Normalize(cfg);
                    return cfg;
                }
            }
        }
        catch
        {
            // Повреждённый конфиг — уходим на дефолты.
        }
        return new AppConfig();
    }

    /// <summary>Пишет конфиг атомарно (через temp + move), создавая каталог при необходимости.</summary>
    public static void Save(string path, AppConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(config, Options);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private static void Normalize(AppConfig cfg)
    {
        cfg.Roles ??= AppConfig.DefaultRoles();
        if (cfg.Roles.Count == 0) cfg.Roles = AppConfig.DefaultRoles();
        cfg.InitialCounts ??= new();
        cfg.RingThresholdsMinutes ??= new() { 15, 30, 45 };
        if (cfg.RingThresholdsMinutes.Count < 3) cfg.RingThresholdsMinutes = new() { 15, 30, 45 };
        cfg.MilestonesRub ??= new() { 1000m, 5000m, 10000m, 50000m };
        cfg.Templates ??= new();
        cfg.ManualRates ??= new();
        cfg.Hotkeys ??= new();
        if (cfg.FillMinutes <= 0) cfg.FillMinutes = 60;
        if (cfg.SalaryHoursPerMonth <= 0) cfg.SalaryHoursPerMonth = 160m;
        if (cfg.Overhead < 0) cfg.Overhead = 1.0m;
    }
}
