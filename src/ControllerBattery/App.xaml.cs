using System.Windows;

namespace ControllerBattery;

public partial class App : Application
{
    private readonly CancellationTokenSource _lifetime = new();

    public static bool StartInBackground { get; } = Environment.GetCommandLineArgs()
        .Any(argument => argument.Equals("--background", StringComparison.OrdinalIgnoreCase));

    public static CancellationToken LifetimeToken =>
        ((App)Current)._lifetime.Token;

    public static void CancelLifetime()
    {
        if (Current is App app && !app._lifetime.IsCancellationRequested)
            app._lifetime.Cancel();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CancelLifetime();
        _lifetime.Dispose();
        base.OnExit(e);
    }
}
