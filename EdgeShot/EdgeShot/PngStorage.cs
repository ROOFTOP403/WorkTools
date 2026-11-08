using System.Drawing.Imaging;

namespace EdgeShot;

public static class PngSaveLocation
{
    public static string Resolve(string? configuredDirectory)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Resolve(configuredDirectory, desktop);
    }

    public static string Resolve(string? configuredDirectory, string defaultDirectory)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredDirectory)
            ? defaultDirectory
            : configuredDirectory.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new IOException("기본 PNG 저장 폴더를 찾을 수 없습니다.");
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            if (!string.Equals(candidate, defaultDirectory, StringComparison.Ordinal))
            {
                return Resolve(null, defaultDirectory);
            }

            throw new IOException("PNG 저장 폴더 경로가 올바르지 않습니다.", ex);
        }
    }
}

public static class PngFileWriter
{
    public static string Save(Image image, string directory, DateTime? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new IOException("PNG 저장 폴더를 찾을 수 없습니다.");
        }

        Directory.CreateDirectory(directory);
        var baseName = $"EdgeShot_{(timestamp ?? DateTime.Now):yyyy-MM-dd_HH-mm-ss-fff}";
        var path = Path.Combine(directory, baseName + ".png");
        for (var suffix = 1; File.Exists(path); suffix++)
        {
            path = Path.Combine(directory, $"{baseName}_{suffix}.png");
        }

        image.Save(path, ImageFormat.Png);
        return path;
    }
}
