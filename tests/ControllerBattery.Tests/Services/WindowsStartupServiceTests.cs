using ControllerBattery.Services;

namespace ControllerBattery.Tests.Services;

public sealed class WindowsStartupServiceTests
{
    [Fact]
    public void BuildCommand_QuotesExecutableAndStartsInBackground() =>
        Assert.Equal("\"C:\\Program Files\\Controller Battery\\ControllerBattery.exe\" --background",
            WindowsStartupService.BuildCommand(
                @"C:\Program Files\Controller Battery\ControllerBattery.exe"));

    [Fact]
    public void BuildCommand_RejectsMissingExecutablePath() =>
        Assert.Throws<InvalidOperationException>(() => WindowsStartupService.BuildCommand(null));

    [Fact]
    public void SetEnabled_UsesRegistryBoundary()
    {
        var registry = new FakeRegistry();
        WindowsStartupService.SetEnabled(true, registry, @"C:\Apps\ControllerBattery.exe");
        Assert.Equal("\"C:\\Apps\\ControllerBattery.exe\" --background", registry.Command);
        WindowsStartupService.SetEnabled(false, registry, null);
        Assert.True(registry.Deleted);
    }

    private sealed class FakeRegistry : WindowsStartupService.IWindowsStartupRegistry
    {
        internal string? Command { get; private set; }
        internal bool Deleted { get; private set; }
        public void SetValue(string command) => Command = command;
        public void DeleteValue() => Deleted = true;
    }
}
