using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Tests.Fakes;

internal sealed class FakeControllerActionProvider : IControllerProvider,
    IPowerOffControllerProvider, IAttentionPulseControllerProvider, IControllerLedProvider
{
    public string Id => "actions";
    internal List<string> Calls { get; } = [];
    public Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ControllerDevice>>([]);
    public Task PowerOffAsync(ControllerDevice controller, CancellationToken cancellationToken = default)
        { Calls.Add("power"); return Task.CompletedTask; }
    public Task PulseAsync(ControllerDevice controller, CancellationToken cancellationToken = default)
        { Calls.Add("identify"); return Task.CompletedTask; }
    public Task SetLedColorAsync(ControllerDevice controller, string color, byte brightness = 0,
        CancellationToken cancellationToken = default)
        { Calls.Add($"led:{color}:{brightness}"); return Task.CompletedTask; }
    public Task ResetLedAsync(ControllerDevice controller, CancellationToken cancellationToken = default)
        { Calls.Add("reset"); return Task.CompletedTask; }
}
