using ControllerBattery.Services;

namespace ControllerBattery.Tests;

public sealed class AtomicJsonFileTests
{
    [Fact]
    public void Save_LeavesValidFileAndNoTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");

        AtomicJsonFile.Save(path, new Value("saved"));

        Assert.Equal("saved", AtomicJsonFile.Load<Value>(path)?.Text);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void Load_RecoversBackupWhenPrimaryIsCorrupt()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "profiles.json");
        AtomicJsonFile.Save(path, new Value("first"));
        AtomicJsonFile.Save(path, new Value("second"));
        File.WriteAllText(path, "not json");

        Assert.Equal("first", AtomicJsonFile.Load<Value>(path)?.Text);
    }

    [Fact]
    public void Load_UsesBackupWhenPrimaryIsMissing()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "missing.json");
        File.WriteAllText(path + ".bak", "{\"Text\":\"backup\"}");

        Assert.Equal("backup", AtomicJsonFile.Load<Value>(path)?.Text);
    }

    [Fact]
    public void Load_ReturnsDefaultWhenPrimaryAndBackupAreCorrupt()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "corrupt.json");
        File.WriteAllText(path, "bad");
        File.WriteAllText(path + ".bak", "also bad");

        Assert.Null(AtomicJsonFile.Load<Value>(path));
    }

    [Fact]
    public void FailedWrite_DoesNotDestroyPreviousFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        AtomicJsonFile.Save(path, new Value("valid"));
        var cyclic = new Cyclic(); cyclic.Self = cyclic;
        Assert.ThrowsAny<Exception>(() => AtomicJsonFile.Save(path, cyclic));
        Assert.Equal("valid", AtomicJsonFile.Load<Value>(path)?.Text);
    }

    private sealed record Value(string Text);
    private sealed class Cyclic { public Cyclic? Self { get; set; } }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"ControllerBattery.Tests.{Guid.NewGuid():N}");
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
