using Kostyor.Core.Roster;
using Xunit;

namespace Kostyor.Core.Tests;

public class RosterMathTests
{
    [Fact]
    public void RatePerHour_sums_rate_times_count()
    {
        var lines = new[]
        {
            new RoleRate(1500m, 2), // Разработчик ×2
            new RoleRate(3000m, 1), // Тимлид ×1
            new RoleRate(1000m, 1), // Аналитик ×1
        };
        // 1500*2 + 3000 + 1000 = 7000
        Assert.Equal(7000m, RosterMath.RatePerHour(lines));
    }

    [Fact]
    public void RatePerHour_applies_overhead()
    {
        var lines = new[] { new RoleRate(1000m, 2) }; // 2000
        Assert.Equal(2600m, RosterMath.RatePerHour(lines, 1.3m));
        Assert.Equal(4600m, RosterMath.RatePerHour(lines, 2.3m));
    }

    [Fact]
    public void RatePerHour_ignores_zero_and_negative_counts()
    {
        var lines = new[]
        {
            new RoleRate(1000m, 0),
            new RoleRate(2000m, -3),
            new RoleRate(1500m, 2),
        };
        Assert.Equal(3000m, RosterMath.RatePerHour(lines));
    }

    [Fact]
    public void RatePerHour_empty_is_zero()
        => Assert.Equal(0m, RosterMath.RatePerHour(Array.Empty<RoleRate>()));

    [Fact]
    public void Headcount_counts_people()
    {
        var lines = new[] { new RoleRate(1000m, 2), new RoleRate(3000m, 1), new RoleRate(900m, 0) };
        Assert.Equal(3, RosterMath.Headcount(lines));
    }

    [Fact]
    public void SalaryToHourly_default_160_hours()
    {
        // 160000 / 160 = 1000
        Assert.Equal(1000m, RosterMath.SalaryToHourly(160000m));
    }

    [Fact]
    public void SalaryToHourly_custom_hours()
    {
        Assert.Equal(1250m, RosterMath.SalaryToHourly(200000m, 160m));
        Assert.Equal(1000m, RosterMath.SalaryToHourly(150000m, 150m));
    }

    [Fact]
    public void SalaryToHourly_guards_nonpositive()
    {
        Assert.Equal(0m, RosterMath.SalaryToHourly(0m));
        Assert.Equal(0m, RosterMath.SalaryToHourly(-100m));
        Assert.Equal(0m, RosterMath.SalaryToHourly(160000m, 0m));
    }
}
