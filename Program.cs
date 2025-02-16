using OMNI.Forms;
using OMNI.Services;
using OMNI.Services.OCR;
using System.Diagnostics;

namespace OMNI;

internal static class Program
{
    private static Form? _currentForm;
    private static TesseractOCRService? _ocrService;
    private static HotKeyManager? _globalHotKeyManager;
    private static SettingsService? _settingsService;
    private static Form? _dummyForm;
    private static bool _isClosing;
    private static bool _isChangingForms;

    [STAThread]
    static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();

            // Create services
            _ocrService = new TesseractOCRService();
            _settingsService = new SettingsService();

            // Create global hotkey manager attached to an invisible form
            _dummyForm = new Form { Visible = false };
            _globalHotKeyManager = new HotKeyManager(_dummyForm);

            // Register Ctrl+U for UI mode toggle
            _globalHotKeyManager.RegisterHotKey(Keys.U, ToggleUIMode, ctrl: true);

            // Handle application exit
            Application.ApplicationExit += (s, e) =>
            {
                Debug.WriteLine("Starting application cleanup");
                _isClosing = true;
                CleanupResources();
            };

            // Start with normal mode
            _currentForm = new MainForm(_ocrService, _settingsService);
            Application.Run(_currentForm);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Application Error: {ex.Message}\n\nDetails: {ex}",
                          "Fatal Error",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Error);
            Debug.WriteLine($"Fatal application error: {ex}");
        }
    }

    private static async void ToggleUIMode()
    {
        if (_isClosing || _isChangingForms) return;

        Debug.WriteLine("Starting UI mode toggle");
        Form? oldForm = null;
        _isChangingForms = true;

        try
        {
            if (_currentForm == null || _ocrService == null || _settingsService == null)
            {
                Debug.WriteLine("ToggleUIMode: One or more required services are null");
                return;
            }

            // Store reference to current form
            oldForm = _currentForm;

            // Stop capture timer on old form
            if (oldForm is ICaptureForm captureForm)
            {
                captureForm.StopCapture();
            }

            // Create new form
            Form newForm;
            if (_currentForm is CompactUIForm)
            {
                newForm = new MainForm(_ocrService, _settingsService);
            }
            else
            {
                newForm = new CompactUIForm(_ocrService, _settingsService);
            }

            // Apply settings
            var settings = _settingsService.CurrentSettings;
            if (settings.CompactUISize.Width > 0 && settings.CompactUISize.Height > 0)
            {
                newForm.Size = settings.CompactUISize;
            }

            if (settings.CompactUILocation != Point.Empty)
            {
                var screen = Screen.FromPoint(settings.CompactUILocation);
                if (screen != null && screen.Bounds.Contains(settings.CompactUILocation))
                {
                    newForm.StartPosition = FormStartPosition.Manual;
                    newForm.Location = settings.CompactUILocation;
                }
                else
                {
                    Debug.WriteLine($"Invalid form location: {settings.CompactUILocation}");
                    newForm.StartPosition = FormStartPosition.CenterScreen;
                }
            }

            // Show new form
            Debug.WriteLine("Showing new form");
            newForm.Show();
            _currentForm = newForm;

            // Hide old form
            if (oldForm != null && !oldForm.IsDisposed)
            {
                Debug.WriteLine("Hiding old form");
                oldForm.Hide();
            }

            // Give time for the new form to initialize
            await Task.Delay(500);

            // Close old form
            if (oldForm != null && !oldForm.IsDisposed)
            {
                Debug.WriteLine("Closing old form");
                oldForm.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ToggleUIMode: {ex}");
            MessageBox.Show($"Error switching UI modes: {ex.Message}",
                          "UI Toggle Error",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Error);

            // If we failed to switch, try to restore the old form
            if (oldForm != null && !oldForm.IsDisposed)
            {
                oldForm.Show();
                _currentForm = oldForm;
            }
        }
        finally
        {
            _isChangingForms = false;
        }
    }

    private static void CleanupResources()
    {
        try
        {
            Debug.WriteLine("Starting resource cleanup");

            // Stop capture on current form
            if (_currentForm is ICaptureForm captureForm)
            {
                captureForm.StopCapture();
            }

            _globalHotKeyManager?.Dispose();
            _dummyForm?.Dispose();

            // Only dispose OCR service when application is actually closing
            if (_isClosing)
            {
                Debug.WriteLine("Application is closing, disposing OCR service");
                _ocrService?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during cleanup: {ex}");
        }
    }
}