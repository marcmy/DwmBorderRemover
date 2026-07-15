namespace DwmBorderRemover.Services;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DwmBorderRemover");

    internal static string LogPath => Path.Combine(DirectoryPath, "app.log");

    internal static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");

                FileInfo file = new(LogPath);
                if (file.Length > 1_000_000)
                {
                    string oldPath = LogPath + ".old";
                    File.Move(LogPath, oldPath, true);
                }
            }
        }
        catch
        {
            // Logging must never break border handling.
        }
    }
}
