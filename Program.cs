using OMNI.Forms;
using OMNI.Services;
using OMNI.Services.Capture;
using OMNI.Services.OCR;
using OMNI.Services.Update;
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

            // Check and complete any pending updates first
            CheckAndCompletePendingUpdates();

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

            // Set up version checking BEFORE running the application
            SetupVersionChecking();

            // Run the application
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

    /// <summary>
    /// Checks for and completes any pending updates from previous sessions
    /// </summary>
    private static void CheckAndCompletePendingUpdates()
    {
        try
        {
            var updateService = new UpdateInstallerService();
            var pendingUpdateTask = updateService.CheckAndCompletePendingUpdatesAsync();
            pendingUpdateTask.Wait(); // Wait synchronously since we're at app startup

            if (pendingUpdateTask.Result)
            {
                MessageBox.Show(
                    "Updates from a previous installation have been completed successfully.",
                    "Update Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking for pending updates: {ex}");
        }
    }

    private static void SetupVersionChecking()
    {
        // Version checking service
        var versionCheckService = new VersionCheckService(
            "Simplistik78",
            "OMNI",
            includePreReleases: true);

        // Check for updates on startup
        Task.Run(async () => {
            try
            {
                // Add a small delay to let the application start up first
                await Task.Delay(3000);

                versionCheckService.UpdateAvailable += (s, e) => {
                    try
                    {
                        if (_currentForm != null && !_currentForm.IsDisposed)
                        {
                            _currentForm.Invoke(() => {
                                using var updateDialog = new UpdateNotificationDialog(
                                    e.NewVersion,
                                    e.ReleaseUrl,
                                    e.ReleaseNotes);
                                updateDialog.ShowDialog(_currentForm);
                            });
                        }
                        else
                        {
                            Debug.WriteLine("Cannot show update dialog: Current form is null or disposed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error showing update dialog: {ex.Message}");
                    }
                };

                // Always check for updates at startup
                Debug.WriteLine("Checking for updates...");
                await versionCheckService.CheckForUpdatesAsync();
                Debug.WriteLine("Update check completed");

                // Update the last check timestamp
                if (_settingsService != null)
                {
                    var currentSettings = _settingsService.CurrentSettings;
                    currentSettings.LastUpdateCheck = DateTime.Now;
                    _settingsService.SaveSettings(currentSettings);
                }
                else
                {
                    Debug.WriteLine("Warning: Settings service is null when trying to update last check timestamp");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
            }
        });
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