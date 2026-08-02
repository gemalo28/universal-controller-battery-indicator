namespace ControllerBattery.Tests.Fakes;

internal sealed class FakeClock(DateTimeOffset now)
{
    internal DateTimeOffset Now { get; private set; } = now;
    internal void Advance(TimeSpan duration) => Now += duration;
}
