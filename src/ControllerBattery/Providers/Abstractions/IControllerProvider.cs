using ControllerBattery.Models;

namespace ControllerBattery.Providers;

public interface IControllerProvider
{
    string Id { get; }

    Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(
        CancellationToken cancellationToken = default);
}

public interface IProviderScanDiagnostics
{
    IReadOnlyList<ProviderScanDiagnostic> LastScanDiagnostics { get; }
}

public interface IPowerOffControllerProvider
{
    Task PowerOffAsync(
        ControllerDevice controller,
        CancellationToken cancellationToken = default);
}

public interface IAttentionPulseControllerProvider
{
    Task PulseAsync(
        ControllerDevice controller,
        CancellationToken cancellationToken = default);
}

public interface IControllerLedProvider
{
    Task SetLedColorAsync(ControllerDevice controller, string color, byte brightness = 0,
        CancellationToken cancellationToken = default);

    Task ResetLedAsync(ControllerDevice controller,
        CancellationToken cancellationToken = default);
}

internal static class ControllerIdentification
{
    internal const int PulseDurationMilliseconds = 450;
    internal const int RumbleKeepAliveMilliseconds = 50;
}
