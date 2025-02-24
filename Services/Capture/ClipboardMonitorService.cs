using System.Text.RegularExpressions;
using OMNI.Models;
using System.Diagnostics;

namespace OMNI.Services;

public class ClipboardMonitorService : IDisposable
{
    private readonly System.Windows.Forms.Timer _pollTimer;
    private string _lastClipboardText = string.Empty;
    private static readonly Regex JumpLocPattern = new(
        @"/jumploc\s+(-?\d+[.,]\d{2})\s+(-?\d+[.,]\d{2})\s+(-?\d+[.,]\d{2})\s+(-?\d+)",
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

                Debug.WriteLine($"Processing clipboard text: {clipText}");
                var match = JumpLocPattern.Match(clipText);

                if (match.Success)
                {
                    // Log all matched groups for debugging
                    Debug.WriteLine("==================== COORDINATE MATCH DEBUG ====================");
                    Debug.WriteLine($"Full match: {match.Value}");
                    Debug.WriteLine($"Group 1 (X): {match.Groups[1].Value}");
                    Debug.WriteLine($"Group 2 (Z): {match.Groups[2].Value}");
                    Debug.WriteLine($"Group 3 (Y): {match.Groups[3].Value}");
                    Debug.WriteLine($"Group 4 (H): {match.Groups[4].Value}");
                    Debug.WriteLine("System culture: " + System.Globalization.CultureInfo.CurrentCulture.Name);
                    Debug.WriteLine("System decimal separator: " + System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

                    // Parse coordinates from "/jumploc X Z Y H" format
                    // Normalize all decimal separators consistently
                    string NormalizeDecimal(string input) => input.Replace(",", ".");

                    var xStr = NormalizeDecimal(match.Groups[1].Value);
                    var zStr = NormalizeDecimal(match.Groups[2].Value);
                    var yStr = NormalizeDecimal(match.Groups[3].Value);
                    var headingStr = match.Groups[4].Value;

                    Debug.WriteLine($"Normalized values - X: {xStr}, Z: {zStr}, Y: {yStr}, H: {headingStr}");

                    // Try each field separately with detailed logging
                    bool xOk = float.TryParse(xStr, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float x);
                    bool zOk = float.TryParse(zStr, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float z);
                    bool yOk = float.TryParse(yStr, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out float y);
                    bool hOk = int.TryParse(headingStr, out int heading);

                    Debug.WriteLine($"Parse results - X: {xOk} ({x}), Z: {zOk} ({z}), Y: {yOk} ({y}), H: {hOk} ({heading})");

                    if (xOk && zOk && yOk && hOk)
                    {
                        var coordinates = new Coordinates(x, y, heading);
                        Debug.WriteLine($"FINAL Coordinates: X={coordinates.X}, Y={coordinates.Y}, H={coordinates.Heading}");
                        CoordinatesFound?.Invoke(this, coordinates);
                    }
                    else
                    {
                        // Try alternate parsing methods for diagnostic purposes
                        Debug.WriteLine("Attempting alternate parsing methods:");

                        // Try with current culture
                        Debug.WriteLine("--- Current Culture ---");
                        float.TryParse(yStr, out float yCurrentCulture);
                        Debug.WriteLine($"Y with current culture: {yCurrentCulture}");

                        // Try with German culture
                        var germanCulture = new System.Globalization.CultureInfo("de-DE");
                        Debug.WriteLine("--- German Culture ---");
                        float.TryParse(yStr, System.Globalization.NumberStyles.Float, germanCulture, out float yGerman);
                        Debug.WriteLine($"Y with German culture: {yGerman}");

                        // Try Double parsing (sometimes has better tolerance)
                        Debug.WriteLine("--- Using Double ---");
                        if (double.TryParse(yStr, System.Globalization.NumberStyles.Float,
                                           System.Globalization.CultureInfo.InvariantCulture, out double yDouble))
                        {
                            Debug.WriteLine($"Y as double: {yDouble}");
                        }

                        // Try manual parsing
                        Debug.WriteLine("--- Manual Parsing ---");
                        try
                        {
                            string[] parts = yStr.Split('.');
                            if (parts.Length == 2)
                            {
                                int integerPart = int.Parse(parts[0]);
                                int decimalPart = int.Parse(parts[1]);
                                float manualResult = integerPart + (decimalPart / 100.0f);
                                Debug.WriteLine($"Y manual parse: {manualResult}");
                            }
                        }
                        catch { }

                        Debug.WriteLine("======== END COORDINATE DEBUG ========");
                        Debug.WriteLine($"Failed to parse coordinates - X:{xStr}, Z:{zStr}, Y:{yStr}, H:{headingStr}");
                    }
                }
                else
                {
                    Debug.WriteLine("Clipboard text did not match expected format");
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