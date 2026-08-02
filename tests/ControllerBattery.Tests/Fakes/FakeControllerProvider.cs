using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Tests.Fakes;

internal sealed class FakeControllerProvider : IControllerProvider
{
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<ControllerDevice>>>> _scans = new();
    private int _active;
    internal FakeControllerProvider(string id = "fake") => Id = id;
    public string Id { get; }
    internal int ScanCount { get; private set; }
    internal int MaximumConcurrentScans { get; private set; }

    internal void Enqueue(Func<CancellationToken, Task<IReadOnlyList<ControllerDevice>>> scan) =>
        _scans.Enqueue(scan);

    public async Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(
        CancellationToken cancellationToken = default)
    {
        ScanCount++;
        MaximumConcurrentScans = Math.Max(MaximumConcurrentScans, Interlocked.Increment(ref _active));
        try { return await _scans.Dequeue()(cancellationToken); }
        finally { Interlocked.Decrement(ref _active); }
    }
}
