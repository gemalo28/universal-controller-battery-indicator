using ControllerBattery.Models;
using ControllerBattery.Providers;

namespace ControllerBattery.Services;

public sealed class ControllerActionService(IControllerProvider provider)
{
    public Task PowerOffAsync(ControllerDevice controller, CancellationToken cancellationToken)
    {
        EnsureSupported(controller.CanPowerOff, "Power off", controller);
        return provider is IPowerOffControllerProvider power
            ? power.PowerOffAsync(controller, cancellationToken)
            : UnsupportedProvider("power off", controller);
    }

    public Task IdentifyAsync(ControllerDevice controller, CancellationToken cancellationToken)
    {
        EnsureSupported(controller.CanIdentify, "Identify", controller);
        return provider is IAttentionPulseControllerProvider attention
            ? attention.PulseAsync(controller, cancellationToken)
            : UnsupportedProvider("identification", controller);
    }

    public Task SetLedAsync(ControllerDevice controller, string color, byte brightness,
        CancellationToken cancellationToken)
    {
        EnsureSupported(controller.CanSetLed, "LED control", controller);
        return provider is IControllerLedProvider led
            ? led.SetLedColorAsync(controller, color, brightness, cancellationToken)
            : UnsupportedProvider("LED control", controller);
    }

    public Task ResetLedAsync(ControllerDevice controller, CancellationToken cancellationToken)
    {
        EnsureSupported(controller.CanSetLed, "LED control", controller);
        return provider is IControllerLedProvider led
            ? led.ResetLedAsync(controller, cancellationToken)
            : UnsupportedProvider("LED control", controller);
    }

    private static void EnsureSupported(bool supported, string action, ControllerDevice controller)
    {
        if (!supported)
            throw new NotSupportedException($"{action} is not supported for {controller.Name}.");
    }

    private static Task UnsupportedProvider(string action, ControllerDevice controller) =>
        Task.FromException(new NotSupportedException(
            $"Controller provider '{controller.ProviderId}' does not support {action}."));
}
