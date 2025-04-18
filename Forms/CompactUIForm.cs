using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Web.WebView2.WinForms;
using OMNI.Services.OCR;
using OMNI.Services;
using OMNI.Services.WebView;
using System.Diagnostics;
using System.Threading;
using OMNI.Models;
using OMNI.Services.Capture;
using OMNI.Services.Update;

namespace OMNI.Forms;

public partial class CompactUIForm : Form, ICaptureForm
{
    private readonly IOCRService _ocrService;
    private readonly SettingsService _settingsService;
    private readonly IMapViewerService _mapViewerService;
    private readonly WebView2 _webView;
    private readonly Button _toggleCaptureButton;
    private readonly Button _positionButton;
    private readonly Label _statusLabel;
    private readonly Label _lastCoordinatesLabel;
    private readonly System.Windows.Forms.Timer _captureTimer;
    private readonly Panel _controlStrip;
    private readonly Panel _gripPanel;
    private readonly ClipboardMonitorService _clipboardService;
    private TrackBar _opacitySlider;
    private Label _opacityLabel;
    
    
    private readonly NumericUpDown _captureX = new()
    {
        Minimum = 0,
        Maximum = 3000,
        DecimalPlaces = 0,
        Increment = 1,
        Width = 70,
        Value = 0
    };

    private readonly NumericUpDown _captureY = new()
    {
        Minimum = 0,
        Maximum = 3000,
        DecimalPlaces = 0,
        Increment = 1,
        Width = 70,
        Value = 0
    };

    private readonly NumericUpDown _captureWidth = new()
    {
        Minimum = 50,
        Maximum = 500,
        DecimalPlaces = 0,
        Increment = 1,
        Width = 70,
        Value = 200
    };

    private readonly NumericUpDown _captureHeight = new()
    {
        Minimum = 20,
        Maximum = 200,
        DecimalPlaces = 0,
        Increment = 1,
        Width = 70,
        Value = 30
    };

    private bool _isCapturing;
    private bool _isResizing;
    private Point _dragStartPoint;
    private ResizeDirection _currentResizeDirection;
    private bool _disposed;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private System.Threading.Timer? _saveTimer;
    private readonly object _saveLock = new object();
    private bool _isSaving;
    private const int RESIZE_BORDER = 5;
    
    private enum ResizeDirection
    {
        None,
        Top, Bottom,
        Left, Right,
        TopLeft, TopRight,
        BottomLeft, BottomRight
    }

    public CompactUIForm(IOCRService ocrService, SettingsService settingsService)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Initialize controls first
        _webView = new WebView2();
        _controlStrip = new Panel();
        _toggleCaptureButton = new Button();
        _positionButton = new Button();
        _statusLabel = new Label();
        _lastCoordinatesLabel = new Label();
        _captureTimer = new System.Windows.Forms.Timer();
        _gripPanel = new Panel
        {
            Height = 10,  // Thin strip at the top
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(30, 30, 30),  // Match black form's theme
            Cursor = Cursors.SizeAll  // Always show move cursor
        };

        InitializeComponent();

        _gripPanel.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
            }
        };

        // Initialize services
        _mapViewerService = new MapViewerService(_webView, isCompactMode: true);
        _clipboardService = new ClipboardMonitorService();
        _clipboardService.CoordinatesFound += async (s, coords) => await ProcessCoordinates(coords);
        _clipboardService.StatusChanged += (s, status) =>
        {
            if (!IsDisposed && _statusLabel != null)
            {
                if (_statusLabel.InvokeRequired)
                {
                    _statusLabel.Invoke(() => _statusLabel.Text = status);
                }
                else
                {
                    _statusLabel.Text = status;
                }
            }
        };
        //opacity slider controls for mini map
        _opacitySlider = new TrackBar
        {
            Minimum = 20,      // 20% minimum opacity
            Maximum = 100,     // 100% maximum opacity
            Value = 100,       // Default to 100% opacity
            TickFrequency = 10,
            Width = 100,
            Height = 20,
            Dock = DockStyle.None,
            AutoSize = false
        };

        _opacityLabel = new Label
        {
            Text = "Opacity:",
            AutoSize = true,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleRight
        };
        var opacityPanel = new Panel
        {
            Height = 30,
            Dock = DockStyle.None,
            AutoSize = true
        };
        SetupCustomComponents();
        SetupEventHandlers();
        LoadSettings();
    

    // Configure capture timer
    _captureTimer.Interval = Math.Max(1000, _settingsService.CurrentSettings.CaptureInterval);

        // Subscribe to map service events
        _mapViewerService.StatusChanged += (s, status) =>
        {
            if (_statusLabel.InvokeRequired)
            {
                _statusLabel.Invoke(() => _statusLabel.Text = status);
            }
            else
            {
                _statusLabel.Text = status;
            }
            Debug.WriteLine($"Map status: {status}");
        };

        _mapViewerService.ErrorOccurred += (s, ex) =>
        {
            if (_statusLabel.InvokeRequired)
            {
                _statusLabel.Invoke(() => _statusLabel.Text = $"Error: {ex.Message}");
            }
            else
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
            Debug.WriteLine($"Map error: {ex}");
        };
    }

    protected override async void OnLoad(EventArgs e)
    {
        await SetMapOpacity(_opacitySlider.Value / 100.0f);
        base.OnLoad(e);
        try
        {
            // Wait for map initialization before proceeding
            await _mapViewerService.WaitForInitializationAsync();

            // Make sure to load the auto-center setting
            bool autoCenter = _settingsService.CurrentSettings.AutoCenterMap;
            Debug.WriteLine($"CompactUIForm loading with AutoCenterMap: {autoCenter}");

            // Apply auto-center setting from saved settings
            await _mapViewerService.SetAutoCenterAsync(autoCenter);
            Debug.WriteLine($"Applied AutoCenterMap setting to map: {autoCenter}");

            // Update context menu to reflect setting
            if (_controlStrip.ContextMenuStrip != null)
            {
                foreach (var item in _controlStrip.ContextMenuStrip.Items)
                {
                    if (item is ToolStripMenuItem menuItem && menuItem.Text == "Auto-Center Map for Compact UI")
                    {
                        menuItem.Checked = autoCenter;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error initializing WebView2: {ex.Message}\nPlease ensure WebView2 Runtime is installed.",
                "WebView2 Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SetupCustomComponents()
    {
        // Form settings
        this.MinimumSize = new Size(400, 300);
        this.Size = new Size(600, 450);
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Color.Black;
        this.Padding = new Padding(1);
        this.TopMost = true;

        // Add and configure grip panel
        this.Controls.Add(_gripPanel);
        _gripPanel.BringToFront();

        // Setup context menu for control strip
        var contextMenu = new ContextMenuStrip();
        var mapMenu = new ToolStripMenuItem("Select Map");
        mapMenu.DropDownItems.AddRange(new ToolStripMenuItem[]
        {
        new ToolStripMenuItem("World Map", null, (s, e) => { _ = SwitchMap(1); }),
        new ToolStripMenuItem("Halnir Cave", null, (s, e) => { _ = SwitchMap(2); }),
        new ToolStripMenuItem("Goblin Caves", null, (s, e) => { _ = SwitchMap(3); })
        });

        // Add map menu to main context menu FIRST
        contextMenu.Items.Add(mapMenu);

        // Opacity submenu to context menu
        var opacityMenu = new ToolStripMenuItem("Map Opacity");

        // preset opacity options
        foreach (var opacity in new[] { 20, 40, 60, 80, 100 })
        {
            var item = new ToolStripMenuItem($"{opacity}%");
            item.Click += (s, e) =>
            {
                _opacitySlider.Value = opacity;
            };
            opacityMenu.DropDownItems.Add(item);
        }

        // Add opacity menu to main context menu
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(opacityMenu);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Check for Updates", null, async (s, e) => await CheckForUpdatesManually()));

        // Control strip setup
        _controlStrip.Height = 40;
        _controlStrip.Dock = DockStyle.Top;
        _controlStrip.BackColor = Color.FromArgb(30, 30, 30);
        _controlStrip.Padding = new Padding(5);
        _controlStrip.ContextMenuStrip = contextMenu;

        // Also attach context menu to grip panel so right-clicking there works too
        _gripPanel.ContextMenuStrip = contextMenu;

        // Set up the WebView2 control
        _webView.Dock = DockStyle.Fill;

        // Add layout panel to manage WebView
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = Color.FromArgb(30, 30, 30)
        };
        contentPanel.Controls.Add(_webView);

        // Configure buttons
        _toggleCaptureButton.Text = "Start";
        _toggleCaptureButton.Width = 70;
        _toggleCaptureButton.Height = 30;
        _toggleCaptureButton.FlatStyle = FlatStyle.Flat;
        _toggleCaptureButton.BackColor = Color.FromArgb(60, 60, 60);
        _toggleCaptureButton.ForeColor = Color.White;
        _toggleCaptureButton.Location = new Point(10, 5);

        _positionButton.Text = "Position";
        _positionButton.Width = 80;
        _positionButton.Height = 30;
        _positionButton.FlatStyle = FlatStyle.Flat;
        _positionButton.BackColor = Color.FromArgb(60, 60, 60);
        _positionButton.ForeColor = Color.White;
        _positionButton.Location = new Point(_toggleCaptureButton.Right + 5, 5);

        // Configure slider settings
        _opacitySlider.Minimum = 20;
        _opacitySlider.Maximum = 100;
        _opacitySlider.Value = 100;
        _opacitySlider.TickFrequency = 10;
        _opacitySlider.Width = 100;
        _opacitySlider.Height = 30;
        _opacitySlider.Location = new Point(_positionButton.Right + 10, 5);

        // Add tooltip for opacity slider
        var toolTip = new ToolTip();
        toolTip.SetToolTip(_opacitySlider, "Map Opacity");

        //event handler for slider
        _opacitySlider.ValueChanged += OpacitySlider_ValueChanged;

        // Create and add the auto-center menu item
        var autoCenterItem = new ToolStripMenuItem("Auto-Center Map for Compact UI")
        {
            Checked = _settingsService.CurrentSettings.AutoCenterMap,
            CheckOnClick = true
        };
        autoCenterItem.Click += async (s, e) =>
        {
            var settings = _settingsService.CurrentSettings;
            settings.AutoCenterMap = autoCenterItem.Checked;
            _settingsService.SaveSettings(settings);
            await _mapViewerService.SetAutoCenterAsync(autoCenterItem.Checked);
            _statusLabel.Text = $"Auto-center {(autoCenterItem.Checked ? "enabled" : "disabled")}";
        };
        contextMenu.Items.Add(autoCenterItem);
        // Add close button
        var closeButton = new Button
        {
            Text = "×",
            Size = new Size(30, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Location = new Point(_controlStrip.Width - 40, 5),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        // Add controls to control strip (without status labels)
        _controlStrip.Controls.AddRange(new Control[] {
        _toggleCaptureButton,
        _positionButton,
        _opacitySlider,
        closeButton
    });

        // Add controls to form
        this.Controls.AddRange(new Control[] {
        contentPanel,
        _controlStrip
    });

        // Set up close button handler
        closeButton.Click += (s, e) => this.Close();

        // Make form draggable via control strip
        _controlStrip.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left && !IsResizingEdge(e.Location))
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
            }
        };
    }

    private void SetupEventHandlers()
    {
        // Form movement and size handlers
        this.LocationChanged += (s, e) =>
        {
            if (!this.IsDisposed && this.WindowState == FormWindowState.Normal)
            {
                DebouncedSave();
                Debug.WriteLine($"[MOVE] CompactUI Position Update Queued: {this.Location}");
            }
        };

        this.SizeChanged += (s, e) =>
        {
            if (!this.IsDisposed && this.WindowState == FormWindowState.Normal)
            {
                DebouncedSave();
                Debug.WriteLine($"[RESIZE] CompactUI Size Update Queued: {this.Size}");
            }
        };

        // Capture control value change handlers
        _captureX.ValueChanged += (s, e) => DebouncedSave();
        _captureY.ValueChanged += (s, e) => DebouncedSave();
        _captureWidth.ValueChanged += (s, e) => DebouncedSave();
        _captureHeight.ValueChanged += (s, e) => DebouncedSave();

        // Button click handlers
        _toggleCaptureButton.Click += ToggleCaptureButton_Click;
        _positionButton.Click += PositionButton_Click;
        _captureTimer.Tick += CaptureTimer_Tick;

        // Form resizing handlers
        this.MouseDown += Form_MouseDown;
        this.MouseMove += Form_MouseMove;
        this.MouseUp += Form_MouseUp;
    }
    private async Task CheckForUpdatesManually()
    {
        try
        {
            _statusLabel.Text = "Checking for updates...";

            var versionCheckService = new VersionCheckService(
                "Simplistik78",
                "OMNI",
                includePreReleases: true);

            bool updateFound = false;

            versionCheckService.UpdateAvailable += (s, e) => {
                updateFound = true;
                using var updateDialog = new UpdateNotificationDialog(
                    e.NewVersion,
                    e.ReleaseUrl,
                    e.ReleaseNotes);
                updateDialog.ShowDialog(this);
            };

            await versionCheckService.CheckForUpdatesAsync();

            // Update timestamp regardless of result
            var settings = _settingsService.CurrentSettings;
            settings.LastUpdateCheck = DateTime.Now;
            _settingsService.SaveSettings(settings);

            if (!updateFound)
            {
                _statusLabel.Text = "Your application is up to date";
                MessageBox.Show(
                    $"You are using the latest version of OMNI (v{VersionManagerService.GetCurrentVersion()}).",
                    "No Updates Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _statusLabel.Text = "Update available";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Update check failed";
            Debug.WriteLine($"Error checking for updates: {ex.Message}");
            MessageBox.Show(
                $"Error checking for updates: {ex.Message}",
                "Update Check Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
    public void StartCapture()
    {
        if (!_isCapturing)
        {
            _clipboardService.IsEnabled = false; // Disable clipboard monitoring
            _captureTimer.Start();
            _toggleCaptureButton.Text = "Stop";
            _statusLabel.Text = "OCR Capturing...";
            _isCapturing = true;

            // Apply auto-center setting
            _ = _mapViewerService.SetAutoCenterAsync(_settingsService.CurrentSettings.AutoCenterMap);

            Debug.WriteLine("OCR Capture started");
        }
    }

    public void StopCapture()
    {
        if (_isCapturing)
        {
            _captureTimer.Stop();
            _toggleCaptureButton.Text = "Start Capture";
            _statusLabel.Text = "OCR Stopped";
            _isCapturing = false;
            _clipboardService.IsEnabled = true; // Re-enable clipboard monitoring
            Debug.WriteLine("OCR Capture stopped, clipboard monitoring resumed");
        }
    }

    private void ToggleCaptureButton_Click(object? sender, EventArgs e)
    {
        if (!_isCapturing)
        {
            StartCapture();
        }
        else
        {
            StopCapture();
        }
    }

    private void PositionButton_Click(object? sender, EventArgs e)
    {
        var overlayForm = new OverlayCaptureForm(bounds =>
        {
            try
            {
                _captureX.Value = bounds.X;
                _captureY.Value = bounds.Y;
                _captureWidth.Value = bounds.Width;
                _captureHeight.Value = bounds.Height;

                SaveCurrentSettings();
                _ = PerformCapture();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to update capture bounds: {ex}");
                _statusLabel.Text = "Failed to set capture area";
            }
        });

        // Set initial position from current settings
        var currentSettings = _settingsService.CurrentSettings;
        if (Screen.FromPoint(new Point(currentSettings.CaptureX, currentSettings.CaptureY)) != null)
        {
            overlayForm.Location = new Point(currentSettings.CaptureX, currentSettings.CaptureY);
            overlayForm.Size = new Size(currentSettings.CaptureWidth, currentSettings.CaptureHeight);
        }
        else
        {
            overlayForm.StartPosition = FormStartPosition.CenterScreen;
            overlayForm.Size = new Size(200, 50);
        }

        overlayForm.Show();
    }

    private async void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        await PerformCapture();
    }

    private async Task PerformCapture()
    {
        if (this.IsDisposed) return;

        try
        {
            var bounds = new Rectangle(
                (int)_captureX.Value,
                (int)_captureY.Value,
                (int)_captureWidth.Value,
                (int)_captureHeight.Value
            );

            var screen = Screen.FromPoint(new Point(bounds.X, bounds.Y));
            if (screen == null)
            {
                _statusLabel.Text = "Error: Capture area outside screen bounds";
                return;
            }

            if (bounds.Width <= 0 || bounds.Height <= 0 ||
                bounds.Right > screen.Bounds.Right ||
                bounds.Bottom > screen.Bounds.Bottom)
            {
                _statusLabel.Text = "Error: Invalid capture dimensions";
                return;
            }

            using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                try
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Screen capture failed: {ex}");
                    _statusLabel.Text = "Screen capture failed";
                    return;
                }
            }

            var (coordinates, rawText) = await _ocrService.ProcessImageAsync(bitmap);

            if (coordinates != null)
            {
                await ProcessCoordinates(coordinates);
            }
            else
            {
                _statusLabel.Text = "No coordinates found";
                _lastCoordinatesLabel.Text = $"Failed: {rawText}";
                Debug.WriteLine($"OCR failed to find coordinates. Raw text: {rawText}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Capture error: {ex}");
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }
    private async Task ProcessCoordinates(Coordinates coordinates)
    {
        if (this.IsDisposed) return;

        try
        {
            if (coordinates != null)
            {
                // Make sure auto-center setting is applied
                await _mapViewerService.SetAutoCenterAsync(_settingsService.CurrentSettings.AutoCenterMap);

                var result = await _mapViewerService.AddMarkerAsync(
                    coordinates.X,
                    coordinates.Y,
                    coordinates.Heading
                );

                if (result.Contains("Error"))
                {
                    _statusLabel.Text = result;
                    Debug.WriteLine($"Marker placement failed: {result}");
                }
                else
                {
                    _statusLabel.Text = "Pin set";
                    Debug.WriteLine($"Marker placed at X:{coordinates.X} Y:{coordinates.Y} H:{coordinates.Heading}");
                }

                _lastCoordinatesLabel.Text = $"Found: {coordinates}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error processing coordinates: {ex}");
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }
    private async void OpacitySlider_ValueChanged(object? sender, EventArgs e)
    {
        try
        {
            // Get the current opacity value (20-100)
            int opacityValue = _opacitySlider.Value;

            // Scale to 0.2-1.0 range for CSS
            float opacityNormalized = opacityValue / 100.0f;

            // Update the form opacity for the window itself
            this.Opacity = opacityNormalized;

            // Update the map opacity via JavaScript
            await SetMapOpacity(opacityNormalized);

            // Save to settings
            var settings = _settingsService.CurrentSettings;
            settings.CompactUIOpacity = opacityValue;
            _settingsService.SaveSettings(settings);

            Debug.WriteLine($"Map opacity set to {opacityValue}%");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting opacity: {ex.Message}");
        }
    }
    private async Task SetMapOpacity(float opacity)
    {
        try
        {
            // Update only the map opacity, not the entire window
            await _mapViewerService.SetMapOpacity(opacity);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error setting opacity: {ex.Message}");
        }
    }
    private void LoadSettings()
    {
        var settings = _settingsService.CurrentSettings;

        _captureTimer.Interval = Math.Max(1000, settings.CaptureInterval);

        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
            _captureX.Maximum = screen.Bounds.Width;
            _captureY.Maximum = screen.Bounds.Height;
            _captureWidth.Maximum = screen.Bounds.Width;
            _captureHeight.Maximum = screen.Bounds.Height;

            _captureX.Value = Math.Max(_captureX.Minimum, Math.Min(_captureX.Maximum, settings.CaptureX));
            _captureY.Value = Math.Max(_captureY.Minimum, Math.Min(_captureY.Maximum, settings.CaptureY));
            _captureWidth.Value = Math.Max(_captureWidth.Minimum, Math.Min(_captureWidth.Maximum, settings.CaptureWidth));
            _captureHeight.Value = Math.Max(_captureHeight.Minimum, Math.Min(_captureHeight.Maximum, settings.CaptureHeight));
        }

        if (settings.CompactUISize.Width > 0 && settings.CompactUISize.Height > 0)
        {
            var width = Math.Max(this.MinimumSize.Width,
                               Math.Min(screen?.Bounds.Width ?? 1920, settings.CompactUISize.Width));
            var height = Math.Max(this.MinimumSize.Height,
                                Math.Min(screen?.Bounds.Height ?? 1080, settings.CompactUISize.Height));

            this.Size = new Size(width, height);
        }

        if (settings.CompactUIOpacity > 0)
        {
            _opacitySlider.Value = settings.CompactUIOpacity;
            this.Opacity = settings.CompactUIOpacity / 100.0f;
        }
        else
        {
            _opacitySlider.Value = 100; // Default to 100% opacity
        }

        if (settings.CompactUILocation != Point.Empty)
        {
            var targetScreen = Screen.FromPoint(settings.CompactUILocation);
            if (targetScreen != null)
            {
                var x = Math.Max(targetScreen.WorkingArea.X,
                               Math.Min(targetScreen.WorkingArea.Right - this.Width,
                                          settings.CompactUILocation.X));
                var y = Math.Max(targetScreen.WorkingArea.Y,
                               Math.Min(targetScreen.WorkingArea.Bottom - this.Height,
                                          settings.CompactUILocation.Y));

                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(x, y);
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }
        else
        {
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        // Update auto-center menu item with saved setting
        try
        {
            if (_controlStrip.ContextMenuStrip != null)
            {
                foreach (var item in _controlStrip.ContextMenuStrip.Items)
                {
                    if (item is ToolStripMenuItem menuItem && menuItem.Text == "Auto-Center Map")
                    {
                        menuItem.Checked = settings.AutoCenterMap;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating auto-center menu item: {ex.Message}");
        }
    }

    private void SaveCurrentSettings()
    {
        try
        {
            var settings = _settingsService.CurrentSettings;

            // Save capture area settings
            settings.CaptureX = (int)_captureX.Value;
            settings.CaptureY = (int)_captureY.Value;
            settings.CaptureWidth = (int)_captureWidth.Value;
            settings.CaptureHeight = (int)_captureHeight.Value;
            settings.CaptureInterval = _captureTimer.Interval;

            // Save form position and size
            if (!this.IsDisposed && this.WindowState == FormWindowState.Normal)
            {
                settings.CompactUISize = this.Size;
                settings.CompactUILocation = this.Location;
            }

            // Save auto-center setting from context menu
            if (_controlStrip.ContextMenuStrip != null)
            {
                foreach (var item in _controlStrip.ContextMenuStrip.Items)
                {
                    
                    if (item is ToolStripMenuItem menuItem && menuItem.Text == "Auto-Center Map")
                    {
                        settings.AutoCenterMap = menuItem.Checked;
                        break;
                    }
                }
            }

            // Maintain enabled state
            settings.CompactUIEnabled = true;

            // Save opacity setting
            settings.CompactUIOpacity = _opacitySlider.Value;

            _settingsService.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to save settings: {ex.Message}");
            _statusLabel.Text = "Failed to save settings";
        }
    }

    private void DebouncedSave()
    {
        if (_disposed || _isSaving) return;

        lock (_saveLock)
        {
            try
            {
                _saveTimer?.Dispose();

                if (!_disposed)
                {
                    _saveTimer = new System.Threading.Timer(_ =>
                    {
                        try
                        {
                            if (_disposed) return;

                            _isSaving = true;
                            if (!IsDisposed && IsHandleCreated)
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        if (!_disposed)
                                        {
                                            SaveCurrentSettings();
                                        }
                                    }
                                    finally
                                    {
                                        _isSaving = false;
                                    }
                                }));
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // Ignore disposal exceptions
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error in save timer callback: {ex}");
                        }
                        finally
                        {
                            _isSaving = false;
                        }
                    }, null, 250, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scheduling save: {ex}");
                _isSaving = false;
            }
        }
    }
    private async Task SwitchMap(int mapId)
    {
        try
        {
            if (_webView.CoreWebView2 != null)
            {
                // Stop capture while switching maps
                var wasCapturing = _isCapturing;
                if (wasCapturing)
                {
                    StopCapture();
                }

                // Clear existing markers
                await _mapViewerService.ClearMarkersAsync();

                // Navigate to new map
                _webView.CoreWebView2.Navigate($"https://shalazam.info/maps/{mapId}");

                // Update status
                _statusLabel.Text = $"Switched to map {mapId}";

                // Restart capture if it was running
                if (wasCapturing)
                {
                    await Task.Delay(1000); // Give the map time to load

                    // Apply auto-center setting to new map
                    await _mapViewerService.SetAutoCenterAsync(_settingsService.CurrentSettings.AutoCenterMap);

                    StartCapture();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error switching maps: {ex}");
            _statusLabel.Text = "Error switching maps";
        }
    }
    #region Form Resize Handling
    private bool IsResizingEdge(Point mousePosition)
    {
        return GetResizeDirection(mousePosition) != ResizeDirection.None;
    }

    private ResizeDirection GetResizeDirection(Point mousePosition)
    {
        if (mousePosition.Y <= RESIZE_BORDER && mousePosition.X <= RESIZE_BORDER)
            return ResizeDirection.TopLeft;
        if (mousePosition.Y <= RESIZE_BORDER && mousePosition.X >= Width - RESIZE_BORDER)
            return ResizeDirection.TopRight;
        if (mousePosition.Y >= Height - RESIZE_BORDER && mousePosition.X <= RESIZE_BORDER)
            return ResizeDirection.BottomLeft;
        if (mousePosition.Y >= Height - RESIZE_BORDER && mousePosition.X >= Width - RESIZE_BORDER)
            return ResizeDirection.BottomRight;
        if (mousePosition.Y <= RESIZE_BORDER)
            return ResizeDirection.Top;
        if (mousePosition.Y >= Height - RESIZE_BORDER)
            return ResizeDirection.Bottom;
        if (mousePosition.X <= RESIZE_BORDER)
            return ResizeDirection.Left;
        if (mousePosition.X >= Width - RESIZE_BORDER)
            return ResizeDirection.Right;
        return ResizeDirection.None;
    }

    private void Form_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _currentResizeDirection = GetResizeDirection(e.Location);
            if (_currentResizeDirection != ResizeDirection.None)
            {
                _isResizing = true;
            }
            _dragStartPoint = PointToScreen(e.Location);
        }
    }

    private void Form_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isResizing)
        {
            ResizeForm(e);
        }
        else
        {
            ResizeDirection direction = GetResizeDirection(e.Location);
            UpdateCursor(direction);
        }
    }

    private void Form_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isResizing = false;
            _currentResizeDirection = ResizeDirection.None;
            SaveCurrentSettings();
        }
    }

    private void UpdateCursor(ResizeDirection direction)
    {
        this.Cursor = direction switch
        {
            ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
            ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
            ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
            ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
            _ => Cursors.Default
        };
    }

    private void ResizeForm(MouseEventArgs e)
    {
        Point currentScreenPos = PointToScreen(e.Location);
        int dx = currentScreenPos.X - _dragStartPoint.X;
        int dy = currentScreenPos.Y - _dragStartPoint.Y;

        switch (_currentResizeDirection)
        {
            case ResizeDirection.Left:
            case ResizeDirection.TopLeft:
            case ResizeDirection.BottomLeft:
                Width = Math.Max(MinimumSize.Width, Width - dx);
                Location = new Point(Location.X + dx, Location.Y);
                break;
            case ResizeDirection.Right:
            case ResizeDirection.TopRight:
            case ResizeDirection.BottomRight:
                Width = Math.Max(MinimumSize.Width, Width + dx);
                break;
        }

        switch (_currentResizeDirection)
        {
            case ResizeDirection.Top:
            case ResizeDirection.TopLeft:
            case ResizeDirection.TopRight:
                Height = Math.Max(MinimumSize.Height, Height - dy);
                Location = new Point(Location.X, Location.Y + dy);
                break;
            case ResizeDirection.Bottom:
            case ResizeDirection.BottomLeft:
            case ResizeDirection.BottomRight:
                Height = Math.Max(MinimumSize.Height, Height + dy);
                break;
        }

        _dragStartPoint = currentScreenPos;

        // Update WebView size safely
        if (_webView != null && !_webView.IsDisposed && _webView.Parent != null)
        {
            var newSize = _webView.Parent.ClientSize;
            if (newSize.Width > 0 && newSize.Height > 0)
            {
                _webView.Size = newSize;
            }
        }

        this.Invalidate();
    }
    #endregion

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(60, 60, 60), 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            if (!_disposed && !IsDisposed)
            {
                StopCapture();

                lock (_saveLock)
                {
                    _saveTimer?.Dispose();
                    _saveTimer = null;
                }

                // Update settings before form closes
                if (IsHandleCreated)
                {
                    var settings = _settingsService.CurrentSettings;
                    settings.CompactUIEnabled = false;
                    _settingsService.SaveSettings(settings);
                }

                // Clean up clipboard service
                _clipboardService?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Error during form closing: {ex}");
        }

        base.OnFormClosing(e);
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            StopCapture();
            _captureTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
            _mapViewerService?.Dispose();
            _clipboardService?.Dispose();

            
            components?.Dispose();

            lock (_saveLock)
            {
                _saveTimer?.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    // Static helper class for native methods
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    }
}