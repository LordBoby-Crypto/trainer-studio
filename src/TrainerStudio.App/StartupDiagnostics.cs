using System.IO;
using System.Reflection;

namespace TrainerStudio.App;

internal static class StartupDiagnostics
{
    private const long MaximumLogBytes = 1_000_000;
    private static readonly object SyncRoot = new();
    private static string? logPath;

    public static string LogPath
    {
        get
        {
            lock (SyncRoot)
            {
                return logPath ??= ResolveLogPath();
            }
        }
    }

    public static void BeginSession()
    {
        try
        {
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaximumLogBytes)
            {
                File.Move(LogPath, LogPath + ".previous", overwrite: true);
            }
        }
        catch
        {
            // Logging must never become a new startup failure.
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        Write($"Trainer Studio {version} starting on {Environment.OSVersion}; " +
              $"64-bit process: {Environment.Is64BitProcess}.");
    }

    public static void Write(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(LogPath,
                    $"{DateTimeOffset.Now:O}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics are best-effort and must not interfere with the application.
        }
    }

    public static void WriteException(string context, Exception exception)
        => Write($"{context}{Environment.NewLine}{exception}");

    private static string ResolveLogPath()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Trainer Studio", "Logs");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "startup.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "TrainerStudio-startup.log");
        }
    }
}
