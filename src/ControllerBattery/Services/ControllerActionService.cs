using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Services;

public sealed class ControllerActionService(IControllerProvider provider)
{
    public Task PowerOffAsync(ControllerDevice controller, CancellationToken cancellationToken) =>
        provider is IPowerOffControllerProvider power
            ? power.PowerOffAsync(controller, cancellationToken)
            : Task.FromException(new NotSupportedException(
                "This controller cannot be turned off by its provider."));

    public Task IdentifyAsync(ControllerDevice controller, CancellationToken cancellationToken) =>
        provider is IAttentionPulseControllerProvider attention
            ? attention.PulseAsync(controller, cancellationToken)
            : Task.CompletedTask;

    public Task SetLedAsync(ControllerDevice controller, string color, byte brightness,
        CancellationToken cancellationToken) =>
        provider is IControllerLedProvider led
            ? led.SetLedColorAsync(controller, color, brightness, cancellationToken)
            : Task.FromException(new NotSupportedException(
                "This controller does not expose LED control."));

    public Task ResetLedAsync(ControllerDevice controller, CancellationToken cancellationToken) =>
        provider is IControllerLedProvider led
            ? led.ResetLedAsync(controller, cancellationToken)
            : Task.FromException(new NotSupportedException(
                "This controller does not expose LED control."));
}
