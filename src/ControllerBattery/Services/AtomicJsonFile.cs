using System.IO;
using System.Text.Json;

namespace ControllerBattery.Services;

internal static class AtomicJsonFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    internal static T? Load<T>(string path)
    {
        var backupPath = BackupPath(path);
        try
        {
            if (!File.Exists(path) && File.Exists(backupPath))
                return Read<T>(backupPath);
            return Read<T>(path);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            try { return Read<T>(backupPath); }
            catch (Exception backupException) when (IsRecoverable(backupException)) { return default; }
        }
    }

    internal static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("A persistence path must have a directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(true);
            }

            if (File.Exists(path))
                File.Replace(temporaryPath, path, BackupPath(path), ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, path);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static T? Read<T>(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<T>(File.ReadAllText(path))
        : default;

    private static string BackupPath(string path) => path + ".bak";

    private static bool IsRecoverable(Exception exception) =>
        exception is JsonException or IOException or UnauthorizedAccessException;
}
