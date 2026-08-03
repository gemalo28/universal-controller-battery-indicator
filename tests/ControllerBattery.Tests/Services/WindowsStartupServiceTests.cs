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
}
