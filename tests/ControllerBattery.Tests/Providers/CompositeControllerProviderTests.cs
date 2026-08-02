using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Tests;

public sealed class CompositeControllerProviderTests
{
    [Fact]
    public async Task Scan_IsolatesFailureAndReportsDiagnostics()
    {
        var diagnostics = new List<ProviderScanDiagnostic>();
        var device = new ControllerDevice("1", "good", "Pad", "Test", "Wireless", 50,
            BatteryLevel.Medium, false, DateTime.Now);
        var provider = new CompositeControllerProvider(
            [new StubProvider("bad", _ => throw new IOException("broken")),
             new StubProvider("good", _ => Task.FromResult<IReadOnlyList<ControllerDevice>>([device]))],
            diagnostics.Add);

        var result = await provider.GetControllersAsync(TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(2, diagnostics.Count);
        Assert.Contains(diagnostics, item => item.ProviderId == "bad" &&
            item.Exception is IOException && item.ResultCount == 0);
        Assert.Contains(diagnostics, item => item.ProviderId == "good" &&
            item.Exception is null && item.ResultCount == 1 && item.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Scan_PropagatesRequestedCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var provider = new CompositeControllerProvider(
            [new StubProvider("cancel", token => Task.FromCanceled<IReadOnlyList<ControllerDevice>>(token))]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetControllersAsync(cancellation.Token));
    }

    [Fact]
    public async Task Scan_CombinesProvidersAndDeduplicatesProviderScopedIdentity()
    {
        var first = Device("same", "one", "First");
        var duplicate = Device("same", "one", "Duplicate");
        var otherProvider = Device("same", "two", "Other");
        var provider = new CompositeControllerProvider([
            new StubProvider("one", _ => Task.FromResult<IReadOnlyList<ControllerDevice>>([first, duplicate])),
            new StubProvider("two", _ => Task.FromResult<IReadOnlyList<ControllerDevice>>([otherProvider]))]);

        var result = await provider.GetControllersAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.Name == "First");
        Assert.Contains(result, item => item.ProviderId == "two");
    }

    [Fact]
    public async Task UnsupportedCapabilities_AreDeterministic()
    {
        var controller = Device("one", "plain", "Plain");
        var provider = new CompositeControllerProvider([new StubProvider("plain", _ =>
            Task.FromResult<IReadOnlyList<ControllerDevice>>([controller]))]);
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            provider.PowerOffAsync(controller, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            provider.SetLedColorAsync(controller, "#FFFFFF", cancellationToken:
                TestContext.Current.CancellationToken));
        await provider.PulseAsync(controller, TestContext.Current.CancellationToken);
    }

    private static ControllerDevice Device(string id, string provider, string name) =>
        new(id, provider, name, "Test", "Wireless", 50, BatteryLevel.Medium, false, DateTime.UnixEpoch);

    private sealed class StubProvider(string id,
        Func<CancellationToken, Task<IReadOnlyList<ControllerDevice>>> scan) : IControllerProvider
    {
        public string Id => id;
        public Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(CancellationToken cancellationToken = default) =>
            scan(cancellationToken);
    }
}
