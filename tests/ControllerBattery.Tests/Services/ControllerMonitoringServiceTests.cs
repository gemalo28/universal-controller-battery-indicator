using ControllerBattery.Models;
using ControllerBattery.Services;
using ControllerBattery.Tests.Fakes;
using ControllerBattery.Providers;

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
        await service.StartAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var manual = service.RefreshAsync(TestContext.Current.CancellationToken);
        await firstStarted.Task;
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.SnapshotUpdated += (_, args) =>
        {
            if (args.Controllers.FirstOrDefault()?.Id == "new") updated.SetResult();
        };
        await timer.TickAsync();
        releaseFirst.SetResult();
        await manual;
        await updated.Task.WaitAsync(TestContext.Current.CancellationToken);

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
        await service.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan);
        Assert.Equal(0, errors);
    }

    private static ControllerDevice Device(string id) => new(id, "fake", id, "Test", "Wireless",
        50, BatteryLevel.Medium, false, DateTime.UnixEpoch);

    [Fact]
    public async Task Restart_StopsPreviousLoopBeforeCreatingNext()
    {
        var timer = new FakePollingTimer();
        await using var service = new ControllerMonitoringService(new FakeControllerProvider(), timer);
        var token = TestContext.Current.CancellationToken;
        await service.StartAsync(TimeSpan.FromSeconds(1), token);
        await service.StartAsync(TimeSpan.FromSeconds(2), token);
        await service.StartAsync(TimeSpan.FromSeconds(3), token);
        Assert.Equal(3, timer.CreatedCount);
        Assert.Equal(1, timer.ActiveCount);
        Assert.Equal(1, timer.MaximumActiveCount);
    }

    [Fact]
    public async Task Stop_AllowsPollingToStartAgain()
    {
        var timer = new FakePollingTimer();
        await using var service = new ControllerMonitoringService(new FakeControllerProvider(), timer);
        var token = TestContext.Current.CancellationToken;
        await service.StartAsync(TimeSpan.FromSeconds(1), token);
        await service.StopAsync();
        Assert.Equal(0, timer.ActiveCount);
        await service.StartAsync(TimeSpan.FromSeconds(1), token);
        Assert.Equal(1, timer.ActiveCount);
    }

    [Fact]
    public async Task Dispose_CancelsAndAwaitsActivePollingRefresh()
    {
        var provider = new FakeControllerProvider();
        var timer = new FakePollingTimer();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Enqueue(async token =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { canceled.SetResult(); throw; }
            return [];
        });
        var service = new ControllerMonitoringService(provider, timer);
        await service.StartAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await timer.TickAsync();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await service.DisposeAsync();
        Assert.True(canceled.Task.IsCompletedSuccessfully);
        Assert.Equal(0, timer.ActiveCount);
    }

    [Fact]
    public async Task PartialProviderDiagnostics_ArePublishedAndThenCleared()
    {
        var flaky = new FakeControllerProvider("flaky");
        flaky.Enqueue(_ => throw new IOException("temporary"));
        flaky.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>([Device("recovered")]));
        var good = new FakeControllerProvider("good");
        good.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>([Device("good")]));
        good.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>([Device("good")]));
        var composite = new CompositeControllerProvider([flaky, good], _ => { });
        await using var service = new ControllerMonitoringService(composite);
        ControllerSnapshotEventArgs? latest = null;
        service.SnapshotUpdated += (_, args) => latest = args;

        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.Contains(latest!.ProviderDiagnostics,
            diagnostic => diagnostic.ProviderId == "flaky" && diagnostic.Exception is IOException);
        Assert.Contains(service.LatestSnapshot, controller => controller.Id == "good");

        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.All(latest!.ProviderDiagnostics, diagnostic => Assert.Null(diagnostic.Exception));
    }
}
