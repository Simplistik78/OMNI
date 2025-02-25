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
    public bool IsDarkMode { get; set; } = true; // Default to dark mode, cause your eyes will appreciate it.
    public bool AutoCheckForUpdates { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
    
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

    public event EventHandler<Settings>? SettingsChanged;

    public SettingsService(string settingsPath = "settings.json")
    {
        _settingsPath = settingsPath;
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
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            });
            File.WriteAllText(_settingsPath, json);
            _currentSettings = settings;
            SettingsChanged?.Invoke(this, settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    private Settings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
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
