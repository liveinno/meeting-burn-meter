namespace Kostyor.Core.Tests;

/// <summary>Управляемые часы для детерминированных тестов счётчика времени/денег.</summary>
internal sealed class TestClock
{
    public DateTimeOffset Now { get; private set; }

    public TestClock(DateTimeOffset start) => Now = start;

    public TestClock() : this(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)) { }

    public DateTimeOffset Get() => Now;

    public void Advance(TimeSpan by) => Now += by;

    public void AdvanceSeconds(double seconds) => Now += TimeSpan.FromSeconds(seconds);
}
