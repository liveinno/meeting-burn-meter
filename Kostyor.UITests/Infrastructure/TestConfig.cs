using System.Text.Json;

namespace Kostyor.UITests.Infrastructure;

/// <summary>Конфиг тестера: путь к exe + список Vision-провайдеров (ТЗ_UI-тестер).</summary>
public sealed class TestConfig
{
    public string ExePath { get; set; } = "";
    public List<VisionProvider> VisionProviders { get; set; } = new();

    public static TestConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<TestConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return cfg ?? throw new InvalidOperationException("Пустой config.test.json");
    }
}

public sealed class VisionProvider
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string VisionModel { get; set; } = "";
}
