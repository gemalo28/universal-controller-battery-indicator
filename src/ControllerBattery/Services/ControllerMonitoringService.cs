using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Services;

public sealed class ControllerMonitoringService : IAsyncDisposable
{
    private readonly IControllerProvider _provider;
    private readonly IPollingTimerFactory _timerFactory;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly SemaphoreSlim _pollingGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource? _pollingCancellation;
    private Task? _pollingTask;
    private long _latestRequest;
    private int _disposeStarted;

    public ControllerMonitoringService(IControllerProvider provider,
        IPollingTimerFactory? timerFactory = null)
    {
        _provider = provider;
        _timerFactory = timerFactory ?? new PeriodicPollingTimerFactory();
    }

    public IReadOnlyList<ControllerDevice> LatestSnapshot { get; private set; } = [];
    public IReadOnlyList<ProviderScanDiagnostic> LatestProviderDiagnostics { get; private set; } = [];
    public bool IsScanning { get; private set; }
    public Exception? LastError { get; private set; }
    /// <summary>Raised on the scan thread. UI subscribers must marshal work to their UI thread.</summary>
    public event EventHandler<ControllerSnapshotEventArgs>? SnapshotUpdated;
    /// <summary>Raised on the scan thread. UI subscribers must marshal work to their UI thread.</summary>
    public event EventHandler<ControllerScanErrorEventArgs>? ScanFailed;

    public async Task StartAsync(TimeSpan pollingInterval, CancellationToken applicationToken)
    {
        await _pollingGate.WaitAsync(applicationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
            await StopPollingCoreAsync().ConfigureAwait(false);
            _pollingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _shutdown.Token, applicationToken);
            _pollingTask = PollAsync(pollingInterval, _pollingCancellation.Token);
        }
        finally
        {
            _pollingGate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _pollingGate.WaitAsync().ConfigureAwait(false);
        try { await StopPollingCoreAsync().ConfigureAwait(false); }
        finally { _pollingGate.Release(); }
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
            LatestProviderDiagnostics = (_provider as IProviderScanDiagnostics)?.LastScanDiagnostics ?? [];
            LastError = null;
            SnapshotUpdated?.Invoke(this, new(request, LatestSnapshot, LatestProviderDiagnostics));
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;
        await _pollingGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _shutdown.Cancel();
            await StopPollingCoreAsync().ConfigureAwait(false);
        }
        finally { _pollingGate.Release(); }

        await _scanGate.WaitAsync().ConfigureAwait(false);
        _scanGate.Release();
        _shutdown.Dispose();
        _pollingGate.Dispose();
        _scanGate.Dispose();
    }

    private async Task StopPollingCoreAsync()
    {
        var cancellation = _pollingCancellation;
        var task = _pollingTask;
        _pollingCancellation = null;
        _pollingTask = null;
        if (cancellation is null) return;
        cancellation.Cancel();
        if (task is not null)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        cancellation.Dispose();
    }

    private async Task PollAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        await using (var timer = _timerFactory.Create(interval))
        {
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        }
    }
}

public sealed record ControllerSnapshotEventArgs(long Sequence,
    IReadOnlyList<ControllerDevice> Controllers,
    IReadOnlyList<ProviderScanDiagnostic> ProviderDiagnostics);
public sealed record ControllerScanErrorEventArgs(Exception Exception);
