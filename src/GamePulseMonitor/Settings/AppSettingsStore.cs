using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamePulseMonitor.Settings;

internal sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    static AppSettingsStore()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly object _gate = new();

    public AppSettingsStore()
    {
        FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GamePulseMonitor",
            "settings.json");
        Current = Load();
    }

    public string FilePath { get; }
    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? SettingsChanged;

    public void Update(Action<AppSettings> update)
    {
        AppSettings snapshot;
        lock (_gate)
        {
            var next = Current.Clone();
            update(next);
            next.Display.Normalize();
            Current = next;
            Save(next);
            snapshot = next.Clone();
        }

        SettingsChanged?.Invoke(this, snapshot);
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Display ??= new DisplaySettings();
            settings.Display.Normalize();
            settings.Hotkeys ??= HotkeySettings.Default();
            settings.Hotkeys.ToggleOverlay ??= HotkeySettings.Default().ToggleOverlay;
            settings.Hotkeys.Exit ??= HotkeySettings.Default().Exit;
            settings.Hotkeys.ToggleBenchmark ??= HotkeySettings.Default().ToggleBenchmark;
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
