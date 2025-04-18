using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace OMNI.Services;

public class Settings
{
    public int CaptureX { get; set; }
    public int CaptureY { get; set; }
    public int CaptureWidth { get; set; }
    public int CaptureHeight { get; set; }
    public int CaptureInterval { get; set; } = 1000;

    // Main Form UI settings
    public Size MainFormSize { get; set; } = new Size(1024, 968);
    public Point MainFormLocation { get; set; } = new Point(100, 100);
    public bool IsDarkMode { get; set; } = true; // Default to dark mode
    public bool AutoCheckForUpdates { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

    // New property for map auto-centering
    public bool AutoCenterMap { get; set; } = true; // Default to auto-centering behavior

    // Compact UI specific settings
    public bool CompactUIEnabled { get; set; } = false;
    public Point CompactUILocation { get; set; } = new Point(100, 100);
    public Size CompactUISize { get; set; } = new Size(400, 300);
    public int CompactUIOpacity { get; set; } = 100;
}


public class SettingsService
{
    private readonly string _settingsPath;
    private Settings _currentSettings;
    private bool _autoCenterMap = true;

    public event EventHandler<Settings>? SettingsChanged;

    public SettingsService(string settingsPath = "settings.json")
    {
        _settingsPath = settingsPath;
        Debug.WriteLine($"Settings file path: {Path.GetFullPath(_settingsPath)}");
        _currentSettings = LoadSettings();
    }

    public Settings CurrentSettings => _currentSettings;

    public void SaveSettings(Settings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                
            });
            File.WriteAllText(_settingsPath, json);
            Debug.WriteLine($"Settings saved with AutoCenterMap: {settings.AutoCenterMap}");
            _currentSettings = settings;
            SettingsChanged?.Invoke(this, settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
    public bool AutoCenterMap
    {
        get
        {
            Debug.WriteLine($"[GET] AutoCenterMap value: {_autoCenterMap}");
            return _autoCenterMap;
        }
        set
        {
            Debug.WriteLine($"[SET] AutoCenterMap changing from {_autoCenterMap} to {value}");
            _autoCenterMap = value;
        }
    }
    private Settings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Debug.WriteLine($"[LOAD] Settings content: {json}");

                var settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                Debug.WriteLine($"[LOAD] Deserialized AutoCenterMap: {settings.AutoCenterMap}");
                return settings;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Loading settings: {ex.Message}");
        }

        Debug.WriteLine("[LOAD] Creating new default settings");
        return new Settings();
    }

    public void ApplySettingsToForm(Form form)
    {
        try
        {
            var settings = _currentSettings;
            if (settings != null)
            {
                form.Size = settings.CompactUISize;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = settings.CompactUILocation;
                Debug.WriteLine($"Loaded Compact UI settings: Size={settings.CompactUISize}, Location={settings.CompactUILocation}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error applying settings to form: {ex.Message}");
        }
    }
}