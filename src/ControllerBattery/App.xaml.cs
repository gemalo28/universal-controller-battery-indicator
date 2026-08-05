using System.Windows;
using System.Threading;

namespace ControllerBattery;

public partial class App : Application
{
private readonly CancellationTokenSource _lifetime = new();
    private static Mutex? _singleInstanceMutex;

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

protected override void OnStartup(StartupEventArgs e)
{
    const string mutexName = "Global\\ControllerBatteryAppSingleton";
    bool createdNew;
    _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);
    if (!createdNew)
    {
        MessageBox.Show("Another instance is already running.", "Single Instance", MessageBoxButton.OK, MessageBoxImage.Information);
        Shutdown();
        return;
    }
    base.OnStartup(e);
}
}
