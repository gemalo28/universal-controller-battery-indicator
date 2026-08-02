namespace ControllerBattery.Services;

public interface IPollingTimer : IAsyncDisposable
{
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
}

public interface IPollingTimerFactory
{
    IPollingTimer Create(TimeSpan interval);
}

internal sealed class PeriodicPollingTimerFactory : IPollingTimerFactory
{
    public IPollingTimer Create(TimeSpan interval) => new PeriodicPollingTimer(interval);
}

internal sealed class PeriodicPollingTimer(TimeSpan interval) : IPollingTimer
{
    private readonly PeriodicTimer _timer = new(interval);
    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) =>
        _timer.WaitForNextTickAsync(cancellationToken);
    public ValueTask DisposeAsync() { _timer.Dispose(); return ValueTask.CompletedTask; }
}
