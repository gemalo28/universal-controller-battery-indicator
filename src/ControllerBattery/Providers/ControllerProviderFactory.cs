namespace ControllerBattery.Providers;

internal static class ControllerProviderFactory
{
    internal static IControllerProvider CreateHardwareProvider() =>
        new CompositeControllerProvider(
        [
            new XInputControllerProvider(),
            new DualSenseHidProvider(),
            new EightBitDoHidProvider(),
            new NintendoSwitchProHidProvider()
        ]);
}
