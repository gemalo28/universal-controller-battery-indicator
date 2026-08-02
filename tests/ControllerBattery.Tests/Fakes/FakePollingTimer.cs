using System.Threading.Channels;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Fakes;

internal sealed class FakePollingTimer : IPollingTimer, IPollingTimerFactory
{
    private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();
    public IPollingTimer Create(TimeSpan interval) => this;
    internal void Tick() => _ticks.Writer.TryWrite(true);
    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) =>
        _ticks.Reader.ReadAsync(cancellationToken);
    public ValueTask DisposeAsync() { _ticks.Writer.TryComplete(); return ValueTask.CompletedTask; }
}
