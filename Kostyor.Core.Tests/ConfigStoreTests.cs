using Kostyor.Core.Config;
using Kostyor.Core.Money;
using Xunit;

namespace Kostyor.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ConfigStoreTests()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "config.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* тестовый temp */ }
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var cfg = ConfigStore.Load(_path);
        Assert.Equal(8, cfg.Roles.Count);
        Assert.Equal(1.0m, cfg.Overhead);
        Assert.Equal(60, cfg.FillMinutes);
        Assert.Contains(cfg.Roles, r => r.Id == "clevel" && r.RatePerHour == 6000m);
    }

    [Fact]
    public void Corrupt_file_returns_defaults_not_throws()
    {
        File.WriteAllText(_path, "{ this is not valid json ");
        var cfg = ConfigStore.Load(_path);
        Assert.NotNull(cfg);
        Assert.NotEmpty(cfg.Roles);
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var cfg = new AppConfig { Overhead = 1.7m, FillMinutes = 90 };
        cfg.Templates.Add(new MeetingTemplate { Name = "Дейли", Counts = { ["dev"] = 3 } });
        ConfigStore.Save(_path, cfg);

        var loaded = ConfigStore.Load(_path);
        Assert.Equal(1.7m, loaded.Overhead);
        Assert.Equal(90, loaded.FillMinutes);
        Assert.Single(loaded.Templates);
        Assert.Equal("Дейли", loaded.Templates[0].Name);
        Assert.Equal(3, loaded.Templates[0].Counts["dev"]);
    }

    [Fact]
    public void ManualRates_build_snapshot_with_three_currencies()
    {
        var snap = new AppConfig().ManualRates.ToSnapshot(DateTimeOffset.UnixEpoch);
        Assert.Equal(RatesOrigin.Manual, snap.Origin);
        Assert.True(snap.Has("USD"));
        Assert.True(snap.Has("EUR"));
        Assert.True(snap.Has("CNY"));
    }

    [Fact]
    public void Normalize_repairs_degenerate_values()
    {
        File.WriteAllText(_path, """{ "Overhead": -1, "FillMinutes": 0, "SalaryHoursPerMonth": 0, "RingThresholdsMinutes": [10] }""");
        var cfg = ConfigStore.Load(_path);
        Assert.Equal(1.0m, cfg.Overhead);
        Assert.Equal(60, cfg.FillMinutes);
        Assert.Equal(160m, cfg.SalaryHoursPerMonth);
        Assert.Equal(3, cfg.RingThresholdsMinutes.Count);
    }
}
