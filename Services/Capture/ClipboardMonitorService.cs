using System.Text.RegularExpressions;
using OMNI.Models;
using System.Diagnostics;

namespace OMNI.Services;

public class ClipboardMonitorService : IDisposable
{
    private readonly System.Windows.Forms.Timer _pollTimer;
    private string _lastClipboardText = string.Empty;
    private static readonly Regex JumpLocPattern = new(
        @"/jumploc\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)",
        RegexOptions.Compiled
    );

    public event EventHandler<Coordinates>? CoordinatesFound;
    public event EventHandler<string>? StatusChanged;

    private bool _isEnabled;
    private bool _disposed;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ClipboardMonitorService));
            _isEnabled = value;
            if (_isEnabled)
            {
                _pollTimer.Start();
                OnStatusChanged("Clipboard monitoring active");
            }
            else
            {
                _pollTimer.Stop();
                OnStatusChanged("Clipboard monitoring stopped");
            }
        }
    }

    public ClipboardMonitorService(int pollInterval = 500)
    {
        Debug.WriteLine("Initializing ClipboardMonitorService");
        _pollTimer = new System.Windows.Forms.Timer
        {
            Interval = pollInterval
        };
        _pollTimer.Tick += PollClipboard;
        IsEnabled = true; // Start monitoring by default
    }

    private void PollClipboard(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            _pollTimer.Stop();
            return;
        }

        try
        {
            if (!IsEnabled) return;

            string clipText = System.Windows.Forms.Clipboard.GetText();

            // Only process if clipboard content has changed
            if (clipText != _lastClipboardText)
            {
                _lastClipboardText = clipText;

                if (string.IsNullOrWhiteSpace(clipText))
                    return;

                var match = JumpLocPattern.Match(clipText);
                if (match.Success)
                {
                    if (float.TryParse(match.Groups[1].Value, out float x) &&
                        float.TryParse(match.Groups[2].Value, out float z) &&
                        float.TryParse(match.Groups[3].Value, out float y) &&
                        float.TryParse(match.Groups[4].Value, out float heading))
                    {
                        var coordinates = new Coordinates(x, y, heading);
                        Debug.WriteLine($"Clipboard coordinates found: {coordinates}");
                        CoordinatesFound?.Invoke(this, coordinates);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error polling clipboard: {ex}");
            OnStatusChanged($"Clipboard error: {ex.Message}");
        }
    }

    protected virtual void OnStatusChanged(string status)
    {
        if (_disposed) return;
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _pollTimer.Stop();
            _pollTimer.Dispose();
        }
    }
}