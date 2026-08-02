using ControllerBattery.Models;
using ControllerBattery.Services;
using ControllerBattery.Tests.Fakes;

namespace ControllerBattery.Tests.Services;

public sealed class ControllerMonitoringServiceTests
{
    [Fact]
    public async Task ManualAndTimerRefreshes_DoNotOverlap_AndNewestRequestWins()
    {
        var provider = new FakeControllerProvider();
        var timer = new FakePollingTimer();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Enqueue(async token =>
        {
            firstStarted.SetResult();
            await releaseFirst.Task.WaitAsync(token);
            return [Device("old")];
        });
        provider.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>([Device("new")]));
        await using var service = new ControllerMonitoringService(provider, timer);
        service.Start(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var manual = service.RefreshAsync(TestContext.Current.CancellationToken);
        await firstStarted.Task;
        timer.Tick();
        await WaitUntilAsync(() => provider.ScanCount == 1);
        releaseFirst.SetResult();
        await manual;
        await WaitUntilAsync(() => service.LatestSnapshot.FirstOrDefault()?.Id == "new");

        Assert.Equal(1, provider.MaximumConcurrentScans);
        Assert.Equal("new", Assert.Single(service.LatestSnapshot).Id);
    }

    [Fact]
    public async Task SuccessfulScan_PublishesSnapshot()
    {
        var provider = new FakeControllerProvider();
        provider.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>([Device("one")]));
        await using var service = new ControllerMonitoringService(provider);
        ControllerSnapshotEventArgs? update = null;
        service.SnapshotUpdated += (_, args) => update = args;

        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.Equal("one", Assert.Single(update!.Controllers).Id);
    }

    [Fact]
    public async Task Failure_PreservesSnapshotAndPublishesError()
    {
        var provider = new FakeControllerProvider();
        provider.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>([Device("good")]));
        provider.Enqueue(_ => throw new IOException("scan failed"));
        await using var service = new ControllerMonitoringService(provider);
        Exception? error = null;
        service.ScanFailed += (_, args) => error = args.Exception;
        await service.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.Equal("good", Assert.Single(service.LatestSnapshot).Id);
        Assert.IsType<IOException>(error);
    }

    [Fact]
    public async Task Shutdown_CancelsActiveScanWithoutPublishingError()
    {
        var provider = new FakeControllerProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Enqueue(async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return []; });
        await using var service = new ControllerMonitoringService(provider);
        var errors = 0;
        service.ScanFailed += (_, _) => errors++;
#pragma warning disable xUnit1051 // Intentionally independent: Stop() is the cancellation under test.
        var scan = service.RefreshAsync();
#pragma warning restore xUnit1051
        await started.Task;
        service.Stop();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan);
        Assert.Equal(0, errors);
    }

    private static ControllerDevice Device(string id) => new(id, "fake", id, "Test", "Wireless",
        50, BatteryLevel.Medium, false, DateTime.UnixEpoch);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}
