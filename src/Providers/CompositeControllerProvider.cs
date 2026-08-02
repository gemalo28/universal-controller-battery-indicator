using ControllerBattery.Models;

namespace ControllerBattery.Providers;

/// <summary>
/// Aggregates independent protocol backends. Failure in one optional backend does
/// not prevent controller families supported by another backend from appearing.
/// </summary>
public sealed class CompositeControllerProvider(IEnumerable<IControllerProvider> providers)
    : IControllerProvider, IPowerOffControllerProvider, IAttentionPulseControllerProvider
{
    private readonly IReadOnlyList<IControllerProvider> _providers = providers.ToArray();

    public string Id => "composite";

    public async Task<IReadOnlyList<ControllerDevice>> GetControllersAsync(
        CancellationToken cancellationToken = default)
    {
        var scans = _providers.Select(provider => ScanSafelyAsync(provider, cancellationToken));
        var observations = (await Task.WhenAll(scans)).SelectMany(result => result);

        return observations
            .GroupBy(device => $"{device.ProviderId}:{device.Id}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    public Task PowerOffAsync(
        ControllerDevice controller,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(candidate =>
            candidate.Id.Equals(controller.ProviderId, StringComparison.OrdinalIgnoreCase));
        if (provider is not IPowerOffControllerProvider powerProvider)
        {
            throw new NotSupportedException("This controller cannot be turned off by its provider.");
        }

        return powerProvider.PowerOffAsync(controller, cancellationToken);
    }

    public Task PulseAsync(
        ControllerDevice controller,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(candidate =>
            candidate.Id.Equals(controller.ProviderId, StringComparison.OrdinalIgnoreCase));
        return provider is IAttentionPulseControllerProvider pulseProvider
            ? pulseProvider.PulseAsync(controller, cancellationToken)
            : Task.CompletedTask;
    }

    private static async Task<IReadOnlyList<ControllerDevice>> ScanSafelyAsync(
        IControllerProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetControllersAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }
}
