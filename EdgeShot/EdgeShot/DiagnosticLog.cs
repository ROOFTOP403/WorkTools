namespace EdgeShot;

internal static class DiagnosticLog
{
    private const long MaximumLogSize = 256 * 1024;
    private static readonly object Sync = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EdgeShot",
        "EdgeShot.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                Directory.CreateDirectory(directory);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaximumLogSize)
                {
                    File.WriteAllText(LogPath, string.Empty);
                }

                File.AppendAllText(LogPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 진단 로그 실패가 캡처를 방해해서는 안 된다.
        }
    }
}
