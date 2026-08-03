using System.Threading.Channels;
using ControllerBattery.Services;

namespace ControllerBattery.Tests.Fakes;

internal sealed class FakePollingTimer : IPollingTimerFactory
{
    private readonly List<TimerInstance> _timers = [];
    internal int CreatedCount => _timers.Count;
    internal int ActiveCount => _timers.Count(timer => !timer.IsDisposed);
    internal int MaximumActiveCount { get; private set; }

    public IPollingTimer Create(TimeSpan interval)
    {
        var timer = new TimerInstance();
        _timers.Add(timer);
        MaximumActiveCount = Math.Max(MaximumActiveCount, ActiveCount);
        return timer;
    }

    internal Task TickAsync() => _timers.Last(timer => !timer.IsDisposed).TickAsync();

    private sealed class TimerInstance : IPollingTimer
    {
        private readonly Channel<TaskCompletionSource> _ticks = Channel.CreateUnbounded<TaskCompletionSource>();
        internal bool IsDisposed { get; private set; }
        internal Task TickAsync()
        {
            var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ticks.Writer.TryWrite(consumed);
            return consumed.Task;
        }
        public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
        {
            var consumed = await _ticks.Reader.ReadAsync(cancellationToken);
            consumed.SetResult();
            return true;
        }
        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            _ticks.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
