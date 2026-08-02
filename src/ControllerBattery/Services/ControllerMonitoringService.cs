using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Services;

public sealed class ControllerMonitoringService : IAsyncDisposable
{
    private readonly IControllerProvider _provider;
    private readonly IPollingTimerFactory _timerFactory;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource? _pollingCancellation;
    private Task? _pollingTask;
    private long _latestRequest;

    public ControllerMonitoringService(IControllerProvider provider,
        IPollingTimerFactory? timerFactory = null)
    {
        _provider = provider;
        _timerFactory = timerFactory ?? new PeriodicPollingTimerFactory();
    }

    public IReadOnlyList<ControllerDevice> LatestSnapshot { get; private set; } = [];
    public bool IsScanning { get; private set; }
    public Exception? LastError { get; private set; }
    public event EventHandler<ControllerSnapshotEventArgs>? SnapshotUpdated;
    public event EventHandler<ControllerScanErrorEventArgs>? ScanFailed;

    public void Start(TimeSpan pollingInterval, CancellationToken applicationToken)
    {
        _pollingCancellation?.Cancel();
        _pollingCancellation?.Dispose();
        _pollingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _shutdown.Token, applicationToken);
        _pollingTask = PollAsync(pollingInterval, _pollingCancellation);
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var request = Interlocked.Increment(ref _latestRequest);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _shutdown.Token);
        var token = linked.Token;
        await _scanGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            IsScanning = true;
            var snapshot = await _provider.GetControllersAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (request != Volatile.Read(ref _latestRequest)) return false;
            LatestSnapshot = snapshot.ToArray();
            LastError = null;
            SnapshotUpdated?.Invoke(this, new(request, LatestSnapshot));
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LastError = exception;
            ScanFailed?.Invoke(this, new(exception));
            return false;
        }
        finally
        {
            IsScanning = false;
            _scanGate.Release();
        }
    }

    public void Stop()
    {
        _pollingCancellation?.Cancel();
        _shutdown.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_pollingTask is not null)
        {
            try { await _pollingTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _shutdown.Dispose();
        _pollingCancellation?.Dispose();
        _scanGate.Dispose();
    }

    private async Task PollAsync(TimeSpan interval, CancellationTokenSource linked)
    {
        await using (var timer = _timerFactory.Create(interval))
        {
            try
            {
                while (await timer.WaitForNextTickAsync(linked.Token).ConfigureAwait(false))
                    await RefreshAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        }
    }
}

public sealed record ControllerSnapshotEventArgs(long Sequence, IReadOnlyList<ControllerDevice> Controllers);
public sealed record ControllerScanErrorEventArgs(Exception Exception);
