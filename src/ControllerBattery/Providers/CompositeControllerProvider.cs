using ControllerBattery.Models;
using System.Diagnostics;
using ControllerBattery.Services;

namespace ControllerBattery.Providers;

/// <summary>
/// Aggregates independent protocol backends. Failure in one optional backend does
/// not prevent controller families supported by another backend from appearing.
/// </summary>
public sealed class CompositeControllerProvider(
    IEnumerable<IControllerProvider> providers,
    Action<ProviderScanDiagnostic>? diagnosticSink = null)
    : IControllerProvider, IPowerOffControllerProvider, IAttentionPulseControllerProvider,
      IControllerLedProvider
{
    private readonly IReadOnlyList<IControllerProvider> _providers = providers.ToArray();
    private readonly Action<ProviderScanDiagnostic> _diagnosticSink = diagnosticSink ?? LogDiagnostic;

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

    public Task SetLedColorAsync(ControllerDevice controller, string color, byte brightness = 0,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(candidate =>
            candidate.Id.Equals(controller.ProviderId, StringComparison.OrdinalIgnoreCase));
        return provider is IControllerLedProvider ledProvider
            ? ledProvider.SetLedColorAsync(controller, color, brightness, cancellationToken)
            : throw new NotSupportedException("This controller does not expose LED control.");
    }

    public Task ResetLedAsync(ControllerDevice controller,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(candidate =>
            candidate.Id.Equals(controller.ProviderId, StringComparison.OrdinalIgnoreCase));
        return provider is IControllerLedProvider ledProvider
            ? ledProvider.ResetLedAsync(controller, cancellationToken)
            : throw new NotSupportedException("This controller does not expose LED control.");
    }

    private async Task<IReadOnlyList<ControllerDevice>> ScanSafelyAsync(
        IControllerProvider provider,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var results = await provider.GetControllersAsync(cancellationToken);
            _diagnosticSink(new(provider.Id, stopwatch.Elapsed, results.Count, null));
            return results;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _diagnosticSink(new(provider.Id, stopwatch.Elapsed, 0, exception));
            return [];
        }
    }

    private static void LogDiagnostic(ProviderScanDiagnostic diagnostic)
    {
        var detail = diagnostic.Exception is null ? "success" : diagnostic.Exception.ToString();
        ApplicationLog.Write($"Controller provider '{diagnostic.ProviderId}': {diagnostic.ResultCount} result(s) " +
            $"in {diagnostic.Duration.TotalMilliseconds:F0} ms; {detail}");
    }
}

public sealed record ProviderScanDiagnostic(
    string ProviderId, TimeSpan Duration, int ResultCount, Exception? Exception);
