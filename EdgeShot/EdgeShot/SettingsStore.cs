using System.Text.Json;

namespace EdgeShot;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath;

    public SettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(appData, "EdgeShot", "settings.json");
    }

    internal SettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.FloatingButtonPlacement ??= new ButtonPlacement();
            if (!Enum.IsDefined(settings.FloatingButtonAccent))
            {
                settings.FloatingButtonAccent = CaptureAccent.Teal;
            }
            if (!Enum.IsDefined(settings.CaptureMode))
            {
                settings.CaptureMode = CaptureMode.Region;
                settings.FullScreenMonitorDeviceName = null;
            }

            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("설정 폴더를 확인할 수 없습니다.");

        Directory.CreateDirectory(directory);
        var tempPath = _settingsPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tempPath, _settingsPath, true);
    }
}
