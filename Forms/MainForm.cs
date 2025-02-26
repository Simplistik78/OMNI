using OMNI.Services;
using OMNI.Services.OCR;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Diagnostics;
using OMNI.Utils;
using OMNI.Services.WebView;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using OMNI.Models;
using System.Drawing;
using System.Windows.Forms;

namespace OMNI.Forms;

public partial class MainForm : Form
{
    private readonly IOCRService _ocrService;
    private readonly WebView2 _webView;
    private readonly IMapViewerService _mapViewerService;
    private readonly Queue<PictureBox> _captureHistory;
    private readonly HotKeyManager _hotKeyManager;
    private readonly SettingsService _settingsService;
    private bool _isCapturing;
    private readonly HashSet<string> _droppedPins = new();
    private CompactUIForm? _compactUI;
    private Coordinates? _lastProcessedCoordinates;
    private DateTime _lastProcessedTime = DateTime.MinValue;
    private const int MinProcessingInterval = 1000; // Minimum time between updates in milliseconds
    private int _currentMapId = 1; // Default to world map
    private readonly ClipboardMonitorService _clipboardService;
    private bool _isDarkMode = true;
    
    // UI Controls initialized directly
    private readonly Panel _controlPanel = new();
    private readonly FlowLayoutPanel _historyPanel = new();
    private readonly NumericUpDown _captureX = new();
    private readonly NumericUpDown _captureY = new();
    private readonly NumericUpDown _captureWidth = new();
    private readonly NumericUpDown _captureHeight = new();
    private readonly Button _startStopButton = new();
    private readonly Button _testCaptureButton = new();
    private readonly Button _resetPinsButton = new();
    private readonly Button _overlayButton = new();
    private readonly Button _compactUIButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _lastCoordinatesLabel = new();
    private readonly System.Windows.Forms.Timer _captureTimer = new();
    private readonly TestCoordinateService _testService;
    private readonly Button _testModeButton;
    public MainForm(IOCRService ocrService, SettingsService settingsService)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Initialize WebView2 and core components
        _webView = new WebView2();
        _mapViewerService = new MapViewerService(_webView, isCompactMode: false);  // Set isCompactMode here
        _captureHistory = new Queue<PictureBox>(10);
        _hotKeyManager = new HotKeyManager(this);
        _clipboardService = new ClipboardMonitorService();
        _clipboardService.CoordinatesFound += async (s, coords) => await ProcessCoordinates(coords);
        _clipboardService.StatusChanged += (s, status) => _statusLabel.Text = status;
        _testService = new TestCoordinateService();
        _testModeButton = new Button
        {
            Text = "Test Mode (F12)",
            Dock = DockStyle.Top,
            Height = 30,
            Margin = new Padding(0, 5, 0, 0),
            FlatStyle = FlatStyle.Standard,
            BackColor = SystemColors.Control
        };



        InitializeComponent();
        SetupFormLayout();
        InitializeCustomControls();
        ConfigureNumericControls();
        LoadSettings();
        SetupEventHandlers();
        SetupHotkeys();
        SetupContextMenu();
        
        
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

        this.FormClosing += MainForm_FormClosing;
    }


    private void SetupFormLayout()
    {
        this.Text = "OMNI – Overlay Map & Navigation Interface";
        this.MinimumSize = new Size(800, 800);
        this.Size = new Size(1024, 968);
        this.StartPosition = FormStartPosition.CenterScreen;

        // Configure panels
        _webView.Dock = DockStyle.Fill;
        _controlPanel.Dock = DockStyle.Left;
        _controlPanel.Width = 280;
        _controlPanel.MinimumSize = new Size(280, 0);
        _controlPanel.MaximumSize = new Size(280, 0);
        _controlPanel.Padding = new Padding(10);
        _controlPanel.AutoScroll = true;

        _historyPanel.Dock = DockStyle.Fill;
        _historyPanel.AutoScroll = true;
        _historyPanel.FlowDirection = FlowDirection.LeftToRight;
        _historyPanel.WrapContents = true;
        _historyPanel.BackColor = Color.White;
        _historyPanel.BorderStyle = BorderStyle.FixedSingle;
        _historyPanel.Padding = new Padding(10);
        _historyPanel.Visible = true;

        var mapContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0)
        };
        mapContainer.Controls.Add(_webView);

        var splitter = new Splitter
        {
            Dock = DockStyle.Left,
            Width = 4
        };

        // controls ordered 
        this.Controls.Add(mapContainer);
        this.Controls.Add(_historyPanel);
        this.Controls.Add(splitter);
        this.Controls.Add(_controlPanel);

        // context menu
        var contextMenu = new ContextMenuStrip();

        // Add map selection menu
        var mapMenu = new ToolStripMenuItem("Select Map");
        mapMenu.DropDownItems.AddRange(new ToolStripMenuItem[]
        {
        new ToolStripMenuItem("World Map", null, (s, e) => { _ = SwitchMap(1); }),
        new ToolStripMenuItem("Halnir Cave", null, (s, e) => { _ = SwitchMap(2); }),
        new ToolStripMenuItem("Goblin Caves", null, (s, e) => { _ = SwitchMap(3); })
        });

        // Add capture interval menu
        var intervalMenu = new ToolStripMenuItem("Capture Interval");
        foreach (var interval in new[] { 500, 1000, 2000, 5000 })
        {
            var item = new ToolStripMenuItem($"{interval}ms");
            item.Click += (s, e) =>
            {
                _captureTimer.Interval = interval;
                SaveCurrentSettings();
            };
            intervalMenu.DropDownItems.Add(item);
        }

        //  menu items
        contextMenu.Items.AddRange(new ToolStripItem[] {
        mapMenu,
        new ToolStripSeparator(),
        intervalMenu,
        new ToolStripSeparator(),
        new ToolStripMenuItem("Reset Position", null, (s, e) => ResetPosition()),
        new ToolStripMenuItem("Show Hotkeys", null, (s, e) => ShowHotkeys()),
        new ToolStripSeparator(),
        new ToolStripMenuItem("Check for Updates", null, async (s, e) => await CheckForUpdatesManually())
    });

        _controlPanel.ContextMenuStrip = contextMenu;
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

                // Update map ID and status
                _currentMapId = mapId;
                _statusLabel.Text = $"Switched to map {mapId}";

                // Reset coordinates since we're changing maps
                _lastProcessedCoordinates = null;

                // Restart capture if it was running
                if (wasCapturing)
                {
                    await Task.Delay(1000); // Give the map time to load
                    await StartCapture();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error switching maps: {ex}");
            _statusLabel.Text = "Error switching maps";
        }
    }
    private void ConfigureNumericControls()
    {
        var screenWidth = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
        var screenHeight = Screen.PrimaryScreen?.Bounds.Height ?? 1080;

        _captureX.Minimum = 0;
        _captureX.Maximum = screenWidth;
        _captureX.Value = 0;

        _captureY.Minimum = 0;
        _captureY.Maximum = screenHeight;
        _captureY.Value = 0;

        _captureWidth.Minimum = 50;
        _captureWidth.Maximum = 500;
        _captureWidth.Value = 200;

        _captureHeight.Minimum = 20;
        _captureHeight.Maximum = 200;
        _captureHeight.Value = 30;
    }

    private void InitializeCustomControls()
    {
        var captureGroupBox = new GroupBox
        {
            Text = "Capture Area",
            Dock = DockStyle.Top,
            Height = 160,
            Padding = new Padding(10)
        };


        var aboutButton = new Button
        {
            Text = "About",
            Dock = DockStyle.Top,
            Height = 20,
            FlatStyle = FlatStyle.Standard,
            BackColor = SystemColors.Control,
            Margin = new Padding(5, 1, 5, 1)
        };

        aboutButton.Click += (s, e) =>
        {
            using var aboutDialog = new AboutDialog();
            aboutDialog.ShowDialog(this);
        };


        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Height = 40;
        _statusLabel.BackColor = Color.FromArgb(240, 240, 240);
        _statusLabel.Padding = new Padding(5);
        _statusLabel.Text = "Map initialized successfully\nNo coordinates captured";

        _lastCoordinatesLabel.AutoSize = true;
        _lastCoordinatesLabel.Dock = DockStyle.Top;
        _lastCoordinatesLabel.Margin = new Padding(0, 5, 0, 0);
        _lastCoordinatesLabel.Text = "No coordinates captured";


        _compactUIButton.Text = "Enable Compact UI";
        _compactUIButton.Dock = DockStyle.Top;
        _compactUIButton.Height = 40;
        _compactUIButton.Margin = new Padding(0, 10, 0, 5);
        _compactUIButton.FlatStyle = FlatStyle.Flat;
        _compactUIButton.BackColor = SystemColors.ControlDark;
        _compactUIButton.ForeColor = Color.White;
        _compactUIButton.Click += CompactUIButton_Click;

        _overlayButton.Text = "Position Capture Window";
        _overlayButton.Dock = DockStyle.Top;
        _overlayButton.Height = 40;
        _overlayButton.Margin = new Padding(0, 5, 5, 5);
        _overlayButton.BackColor = SystemColors.ActiveCaption;
        _overlayButton.FlatStyle = FlatStyle.Standard;

        _resetPinsButton.Text = "Reset All Arrows";
        _resetPinsButton.Dock = DockStyle.Top;
        _resetPinsButton.Height = 30;
        _resetPinsButton.Margin = new Padding(0, 5, 0, 0);
        _resetPinsButton.FlatStyle = FlatStyle.Standard;
        _resetPinsButton.BackColor = Color.LightCoral;
        _resetPinsButton.ForeColor = Color.White;

        _startStopButton.Text = "Start Capture";
        _startStopButton.Dock = DockStyle.Top;
        _startStopButton.Height = 30;
        _startStopButton.Margin = new Padding(0, 5, 0, 0);
        _startStopButton.FlatStyle = FlatStyle.Standard;
        _startStopButton.BackColor = SystemColors.Control;

        
        _testCaptureButton.Text = "Test Capture Area";
        _testCaptureButton.Dock = DockStyle.Top;
        _testCaptureButton.Height = 30;
        _testCaptureButton.Margin = new Padding(0, 5, 0, 0);
        _testCaptureButton.FlatStyle = FlatStyle.Standard;
        _testCaptureButton.BackColor = SystemColors.Control;

        // dark mode toggle button
        var darkModeButton = new Button
        {
            Text = _isDarkMode ? "Light Mode" : "Dark Mode",
            Dock = DockStyle.Top,
            Height = 30,
            Margin = new Padding(0, 5, 0, 0),
            FlatStyle = FlatStyle.Standard,
            BackColor = _isDarkMode ? Color.SlateGray : Color.LightGray,
            ForeColor = _isDarkMode ? Color.White : Color.Black
        };

        darkModeButton.Click += (s, e) => {
            _isDarkMode = !_isDarkMode;
            darkModeButton.Text = _isDarkMode ? "Light Mode" : "Dark Mode";
            darkModeButton.BackColor = _isDarkMode ? Color.SlateGray : Color.LightGray;
            darkModeButton.ForeColor = _isDarkMode ? Color.White : Color.Black;
            ApplyDarkMode(_isDarkMode);
        };

        

        var numericTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(0),
            ColumnStyles = {
            new ColumnStyle(SizeType.Absolute, 60),
            new ColumnStyle(SizeType.Percent, 100)
        }
        };

        numericTable.Controls.Add(new Label { Text = "X:", Anchor = AnchorStyles.Left }, 0, 0);
        numericTable.Controls.Add(_captureX, 1, 0);
        numericTable.Controls.Add(new Label { Text = "Y:", Anchor = AnchorStyles.Left }, 0, 1);
        numericTable.Controls.Add(_captureY, 1, 1);
        numericTable.Controls.Add(new Label { Text = "Width:", Anchor = AnchorStyles.Left }, 0, 2);
        numericTable.Controls.Add(_captureWidth, 1, 2);
        numericTable.Controls.Add(new Label { Text = "Height:", Anchor = AnchorStyles.Left }, 0, 3);
        numericTable.Controls.Add(_captureHeight, 1, 3);

        captureGroupBox.Controls.Add(numericTable);

        _controlPanel.Controls.Clear();
        _controlPanel.Controls.AddRange(new Control[] {
        aboutButton,
        _statusLabel,
        captureGroupBox,           // buttons in order 
        _testCaptureButton,        // Test capture button moved up
        _startStopButton,          // Start/stop capture button
        _resetPinsButton,          // Reset pins button
        _overlayButton,            // Position capture window button
        darkModeButton,            // Added dark mode toggle
        _compactUIButton,          // Compact UI button
        _historyPanel              // History panel
    });

        // Apply initial dark mode setting
        ApplyDarkMode(_isDarkMode);
    }


    private void ApplyDarkMode(bool darkMode)
    {
        if (darkMode)
        {
            // Dark mode
            // Main panel - darker background
            _controlPanel.BackColor = Color.FromArgb(40, 40, 45);

            // History panel - lighter to stand out
            _historyPanel.BackColor = Color.FromArgb(70, 70, 75);

            // Status labels
            _statusLabel.BackColor = Color.FromArgb(55, 55, 60);
            _statusLabel.ForeColor = Color.White;
            _lastCoordinatesLabel.ForeColor = Color.White;

            // Standard button color for most buttons
            Color standardButtonColor = Color.FromArgb(70, 130, 180); // Steel Blue

            
            _startStopButton.BackColor = standardButtonColor;
            _startStopButton.ForeColor = Color.White;

            _testCaptureButton.BackColor = standardButtonColor;
            _testCaptureButton.ForeColor = Color.White;

            
            _resetPinsButton.BackColor = Color.LightCoral;
            _resetPinsButton.ForeColor = Color.White;

            _overlayButton.BackColor = standardButtonColor;
            _overlayButton.ForeColor = Color.White;

            
            
            _compactUIButton.ForeColor = Color.White;

            
            foreach (Control control in _controlPanel.Controls)
            {
                if (control is GroupBox groupBox)
                {
                    groupBox.ForeColor = Color.White;
                    groupBox.BackColor = Color.FromArgb(50, 50, 55);

                    foreach (Control innerControl in groupBox.Controls)
                    {
                        if (innerControl is Label label)
                        {
                            label.ForeColor = Color.White;
                        }
                        else if (innerControl is Button button)
                        {
                            button.BackColor = standardButtonColor;
                            button.ForeColor = Color.White;
                        }
                        else if (innerControl is TableLayoutPanel tableLayoutPanel)
                        {
                            tableLayoutPanel.BackColor = Color.FromArgb(50, 50, 55);
                            foreach (Control tableControl in tableLayoutPanel.Controls)
                            {
                                if (tableControl is Label tableLabel)
                                {
                                    tableLabel.ForeColor = Color.White;
                                    tableLabel.BackColor = Color.FromArgb(50, 50, 55);
                                }
                            }
                        }
                    }
                }
                else if (control is Button button)
                {
                    // Only modify buttons that haven't already been handled individually
                    if (button != _startStopButton && button != _testCaptureButton &&
                        button != _resetPinsButton && button != _overlayButton &&
                        button != _compactUIButton)
                    {
                        button.BackColor = standardButtonColor;
                        button.ForeColor = Color.White;
                    }
                }
            }
        }
        else
        {
            // Light mode
            _controlPanel.BackColor = SystemColors.Control;
            _historyPanel.BackColor = Color.White;
            _statusLabel.BackColor = Color.FromArgb(240, 240, 240);
            _statusLabel.ForeColor = Color.Black;
            _lastCoordinatesLabel.ForeColor = Color.Black;

            // Reset button colors
            _startStopButton.BackColor = SystemColors.Control;
            _startStopButton.ForeColor = Color.Black;

            _testCaptureButton.BackColor = SystemColors.Control;
            _testCaptureButton.ForeColor = Color.Black;

            _resetPinsButton.BackColor = Color.LightCoral;
            _resetPinsButton.ForeColor = Color.White;

            _overlayButton.BackColor = SystemColors.ActiveCaption;
            _overlayButton.ForeColor = Color.Black;

            _compactUIButton.BackColor = SystemColors.ControlDark;
            _compactUIButton.ForeColor = Color.White;

            // Update form elements back to light mode
            foreach (Control control in _controlPanel.Controls)
            {
                if (control is GroupBox groupBox)
                {
                    groupBox.ForeColor = Color.Black;
                    groupBox.BackColor = SystemColors.Control;

                    foreach (Control innerControl in groupBox.Controls)
                    {
                        if (innerControl is Label label)
                        {
                            label.ForeColor = Color.Black;
                        }
                        else if (innerControl is Button button)
                        {
                            button.BackColor = SystemColors.Control;
                            button.ForeColor = Color.Black;
                        }
                        else if (innerControl is TableLayoutPanel tableLayoutPanel)
                        {
                            tableLayoutPanel.BackColor = SystemColors.Control;
                            foreach (Control tableControl in tableLayoutPanel.Controls)
                            {
                                if (tableControl is Label tableLabel)
                                {
                                    tableLabel.ForeColor = Color.Black;
                                    tableLabel.BackColor = SystemColors.Control;
                                }
                            }
                        }
                    }
                }
                else if (control is Button button)
                {
                    // Only modify buttons that haven't already been handled individually
                    if (button != _startStopButton && button != _testCaptureButton &&
                        button != _resetPinsButton && button != _overlayButton &&
                        button != _compactUIButton)
                    {
                        button.BackColor = SystemColors.Control;
                        button.ForeColor = Color.Black;
                    }
                }
            }
        }

        // Save dark mode preference
        var settings = _settingsService.CurrentSettings;
        settings.IsDarkMode = darkMode;
        _settingsService.SaveSettings(settings);
    }
    private void CompactUIButton_Click(object? sender, EventArgs e)
    {
        if (_compactUI == null || _compactUI.IsDisposed)
        {
            // Stop any active captures before creating compact UI
            StopCapture();

            _compactUI = new CompactUIForm(_ocrService, _settingsService);

            // Load settings before showing the form
            var settings = _settingsService.CurrentSettings;

            // Apply saved position and size if they exist
            if (settings.CompactUISize != Size.Empty)
            {
                _compactUI.Size = settings.CompactUISize;
            }

            if (settings.CompactUILocation != Point.Empty)
            {
                var screen = Screen.FromPoint(settings.CompactUILocation);
                if (screen != null && screen.Bounds.Contains(settings.CompactUILocation))
                {
                    _compactUI.StartPosition = FormStartPosition.Manual;
                    _compactUI.Location = settings.CompactUILocation;
                    Debug.WriteLine($"[MAIN] Restored CompactUI at: {settings.CompactUILocation}");
                }
            }

            settings.CompactUIEnabled = true;
            _settingsService.SaveSettings(settings);

            // Hide the main form to prevent interference
            this.Hide();

            _compactUI.Show();
            _compactUIButton.Text = "Disable Compact UI";
            _compactUIButton.BackColor = Color.FromArgb(192, 0, 0);

            // Handle form closing
            _compactUI.FormClosed += (s, e) =>
            {
                var closeSettings = _settingsService.CurrentSettings;
                closeSettings.CompactUIEnabled = false;
                _settingsService.SaveSettings(closeSettings);

                _compactUI = null;
                _compactUIButton.Text = "Enable Compact UI";
                _compactUIButton.BackColor = SystemColors.ControlDark;

                // Show main form again
                this.Show();
            };
        }
        else
        {
            SaveCompactUISettings();
            _compactUI.Close();
            _compactUI = null;
            _compactUIButton.Text = "Enable Compact UI";
            _compactUIButton.BackColor = SystemColors.ControlDark;
        }
    }
    private void SaveCompactUISettings()
    {
        if (_compactUI != null)
        {
            var settings = _settingsService.CurrentSettings;
            settings.CompactUISize = _compactUI.Size;
            settings.CompactUILocation = _compactUI.Location;
            settings.CompactUIEnabled = false;
            _settingsService.SaveSettings(settings);

            Debug.WriteLine($"Saved Compact UI settings: Size={_compactUI.Size}, Location={_compactUI.Location}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _mapViewerService?.Dispose();
            _hotKeyManager?.Dispose();
            _clipboardService?.Dispose();
            ClearCaptureHistory();
        }
        base.Dispose(disposing);
    }
    
    public async Task StartCapture()
    {
        if (_isCapturing) return;

        try
        {
            await _mapViewerService.WaitForInitializationAsync();
            _clipboardService.IsEnabled = false; // Disable clipboard monitoring
            _captureTimer.Start();
            _startStopButton.Text = "Stop";
            _statusLabel.Text = "Capturing...";
            _isCapturing = true;
            Debug.WriteLine("OCR Capture started");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start capture: {ex}");
            _statusLabel.Text = "Failed to start capture";
            MessageBox.Show(
                "Failed to start capture. Please ensure the map is properly initialized.",
                "Capture Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    public void StopCapture()
    {
        if (_isCapturing)
        {
            _captureTimer.Stop();
            _startStopButton.Text = "Start Capture";
            _statusLabel.Text = "Stopped";
            _isCapturing = false;
            _clipboardService.IsEnabled = true; // Re-enable clipboard monitoring
            Debug.WriteLine("OCR Capture stopped, clipboard monitoring resumed");
        }
    }
    
    private async Task ProcessCoordinates(Coordinates coordinates)
    {
        try
        {
            if (coordinates != null && _webView.CoreWebView2 != null)
            {
                if (_lastProcessedCoordinates?.Equals(coordinates) == true &&
                    (DateTime.Now - _lastProcessedTime).TotalMilliseconds < MinProcessingInterval)
                {
                    Debug.WriteLine("Skipping duplicate coordinates");
                    return;
                }

                _lastProcessedCoordinates = coordinates;
                _lastProcessedTime = DateTime.Now;

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


    private void SetupEventHandlers()
    {
        _startStopButton.Click += StartStopButton_Click;
        _testCaptureButton.Click += TestCaptureButton_Click;
        _resetPinsButton.Click += ResetPinsButton_Click;
        _overlayButton.Click += OverlayButton_Click;
        _captureTimer.Tick += CaptureTimer_Tick;
        _testModeButton.Click += TestModeButton_Click;
        _hotKeyManager.RegisterHotKey(Keys.F12, () => TestModeButton_Click(null, null));
        _captureX.ValueChanged += (s, e) => SaveCurrentSettings();
        _captureY.ValueChanged += (s, e) => SaveCurrentSettings();
        _captureWidth.ValueChanged += (s, e) => SaveCurrentSettings();
        _captureHeight.ValueChanged += (s, e) => SaveCurrentSettings();
    }
    private void TestModeButton_Click(object? sender = null, EventArgs? e = null)
    {
        _testService.ToggleTestMode();
        _testModeButton.BackColor = _testService.IsTestModeEnabled ? Color.LightGreen : SystemColors.Control;
        _statusLabel.Text = $"Test mode {(_testService.IsTestModeEnabled ? "enabled" : "disabled")}";
    }
    private void SetupHotkeys()
    {
        _hotKeyManager.RegisterHotKey(Keys.F9, () => StartStopButton_Click(null, EventArgs.Empty));
        _hotKeyManager.RegisterHotKey(Keys.F10, async () => await PerformCapture(), ctrl: true);
        _hotKeyManager.RegisterHotKey(Keys.T, () => TestCaptureButton_Click(null, EventArgs.Empty), ctrl: true);
        _hotKeyManager.RegisterHotKey(Keys.R, () => ResetPinsButton_Click(null, EventArgs.Empty), ctrl: true);
    }

    private async void ResetPinsButton_Click(object? sender, EventArgs e)
    {
        if (_webView.CoreWebView2 != null)
        {
            try
            {
                _statusLabel.Text = "Clearing markers...";
                string script = @"
                (function() {
                    try {
                        // Get base URL without pins
                        let url = window.location.href;
                        let baseUrl = url.split('?')[0];
                        if (!baseUrl.endsWith('/1')) {
                            baseUrl += '/1';
                        }

                        // Reset to base URL without any pins
                        window.location.href = baseUrl;

                        // Clear local tracking
                        window.appMarkers = [];
                        
                        return 'All pins cleared';
                    } catch(e) {
                        console.error('Error in pin cleanup:', e);
                        return 'Error: ' + e.toString();
                    }
                })();
            ";

                var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                result = result.Trim('"');
                _statusLabel.Text = result;

                // Clear our local pin tracking
                _droppedPins.Clear();

                Debug.WriteLine($"Reset pins result: {result}");
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error clearing markers: " + ex.Message;
                Debug.WriteLine($"Error clearing markers: {ex}");
            }
        }
    }

    private async void StartStopButton_Click(object? sender, EventArgs e)
    {
        if (!_isCapturing)
        {
            try
            {
                _startStopButton.Enabled = false;
                _statusLabel.Text = "Starting capture...";

                // Wait for map initialization
                await _mapViewerService.WaitForInitializationAsync();

                _captureTimer.Start();
                _startStopButton.Text = "Stop";
                _statusLabel.Text = "Capturing...";
                _isCapturing = true;
                Debug.WriteLine("Capture started");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start capture: {ex}");
                _statusLabel.Text = "Failed to start capture";
                MessageBox.Show(
                    "Failed to start capture. Please ensure the map is properly initialized.",
                    "Capture Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _startStopButton.Enabled = true;
            }
        }
        else
        {
            StopCapture();
        }
    }

    private async void SingleCaptureButton_Click(object? sender, EventArgs e)
    {
        await PerformCapture();
    }

    private void TestCaptureButton_Click(object? sender, EventArgs e)
    {
        var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point((int)_captureX.Value, (int)_captureY.Value),
            Size = new Size((int)_captureWidth.Value, (int)_captureHeight.Value),
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.Red,
            Opacity = 0.5,
            TopMost = true
        };

        form.Show();
        var timer = new System.Windows.Forms.Timer
        {
            Interval = 2000
        };

        timer.Tick += (s, e) =>
        {
            timer.Stop();
            form.Close();
            form.Dispose();
            timer.Dispose();
        };

        timer.Start();
    }

    private void OverlayButton_Click(object? sender, EventArgs e)
    {
        var overlayForm = new OverlayCaptureForm(bounds =>
        {
            _captureX.Value = bounds.X;
            _captureY.Value = bounds.Y;
            _captureWidth.Value = bounds.Width;
            _captureHeight.Value = bounds.Height;
            SaveCurrentSettings();
            _ = PerformCapture();
        });

        overlayForm.Show();
    }

    private async void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        await PerformCapture();
    }

    private async Task PerformCapture()
    {
        try
        {
            var bounds = new Rectangle(
                (int)_captureX.Value,
                (int)_captureY.Value,
                (int)_captureWidth.Value,
                (int)_captureHeight.Value
            );

            // Validate bounds are within screen limits
            var screen = Screen.FromPoint(new Point(bounds.X, bounds.Y));
            if (screen == null)
            {
                _statusLabel.Text = "Error: Capture area outside screen bounds";
                return;
            }

            // Ensure capture area is within valid bounds
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
                    Debug.WriteLine("Screen area captured successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Screen capture failed: {ex}");
                    _statusLabel.Text = "Screen capture failed";
                    return;
                }
            }

            var (coordinates, rawText) = await _ocrService.ProcessImageAsync(bitmap);
            Debug.WriteLine($"OCR Capture Result - Coordinates: {coordinates}, Raw Text: {rawText}");

            if (coordinates != null && _webView.CoreWebView2 != null)
            {
                bool shouldProcess = true;

                if (_lastProcessedCoordinates != null)
                {
                    if (_lastProcessedCoordinates.Equals(coordinates) ||
                        (DateTime.Now - _lastProcessedTime).TotalMilliseconds < MinProcessingInterval)
                    {
                        shouldProcess = false;
                        _statusLabel.Text = "Skipping duplicate coordinates";
                        Debug.WriteLine($"Skipping coordinates: Current={coordinates}, Last={_lastProcessedCoordinates}");
                        return;
                    }
                }

                if (shouldProcess)
                {
                    // Adjust coordinates based on map type
                    var adjustedCoords = coordinates;

                    switch (_currentMapId)
                    {
                        case 2: // Halnir Cave
                                // TODO: Add coordinate transformation for Halnir Cave
                            Debug.WriteLine($"Halnir Cave coordinates detected: {coordinates}");
                            break;

                        case 1: // World Map
                        case 3: // Goblin Caves
                        default:
                            // Use existing coordinate system
                            break;
                    }

                    _lastProcessedCoordinates = adjustedCoords;
                    _lastProcessedTime = DateTime.Now;

                    Debug.WriteLine("Adding capture to history");
                    AddToHistory(new Bitmap(bitmap));

                    var result = await _mapViewerService.AddMarkerAsync(
                        adjustedCoords.X,
                        adjustedCoords.Y,
                        adjustedCoords.Heading
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

    
    private void SetupContextMenu()
    {
        var contextMenu = new ContextMenuStrip();

        //map selection menu
        var mapMenu = new ToolStripMenuItem("Select Map");
        mapMenu.DropDownItems.AddRange(new ToolStripMenuItem[]
        {
        new ToolStripMenuItem("World Map", null, (s, e) => { _ = SwitchMap(1); }),
        new ToolStripMenuItem("Halnir Cave", null, (s, e) => { _ = SwitchMap(2); }),
        new ToolStripMenuItem("Goblin Caves", null, (s, e) => { _ = SwitchMap(3); })
        });

        // capture interval menu
        var intervalMenu = new ToolStripMenuItem("Capture Interval");
        foreach (var interval in new[] { 500, 1000, 2000, 5000 })
        {
            var item = new ToolStripMenuItem($"{interval}ms");
            item.Click += (s, e) =>
            {
                _captureTimer.Interval = interval;
                SaveCurrentSettings();
            };
            intervalMenu.DropDownItems.Add(item);
        }

        // menu items
        contextMenu.Items.AddRange(new ToolStripItem[] {
        mapMenu,
        new ToolStripSeparator(),
        intervalMenu,
        new ToolStripSeparator(),
        new ToolStripMenuItem("Reset Position", null, (s, e) => ResetPosition()),
        new ToolStripMenuItem("Show Hotkeys", null, (s, e) => ShowHotkeys()),
        new ToolStripSeparator(),
        new ToolStripMenuItem("Check for Updates", null, async (s, e) => await CheckForUpdatesManually())
    });

        _controlPanel.ContextMenuStrip = contextMenu;
    }



    private void AddToHistory(Bitmap capture)
    {
        Debug.WriteLine($"AddToHistory called, Current history count: {_captureHistory.Count}");  
        try
        {
            if (capture == null)
            {
                Debug.WriteLine("Capture is null");  
                return;
            }

            var pictureBox = new PictureBox
            {
                Size = new Size(120, 90),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(5),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };

            try
            {
                pictureBox.Image = capture;
                Debug.WriteLine("Image set to PictureBox");  
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting image: {ex}");
                capture.Dispose();
                pictureBox.Dispose();
                return;
            }

            if (_captureHistory.Count >= 10)
            {
                Debug.WriteLine("Removing old capture from history");  
                var oldPictureBox = _captureHistory.Dequeue();
                _historyPanel.Controls.Remove(oldPictureBox);
                oldPictureBox.Image?.Dispose();
                oldPictureBox.Dispose();
            }

            _captureHistory.Enqueue(pictureBox);
            Debug.WriteLine($"New history count: {_captureHistory.Count}");  

            if (!_historyPanel.IsDisposed)
            {
                if (_historyPanel.InvokeRequired)
                {
                    _historyPanel.Invoke(() => {
                        _historyPanel.Controls.Add(pictureBox);
                        _historyPanel.Refresh();
                        Debug.WriteLine("Added to history panel via Invoke");  
                    });
                }
                else
                {
                    _historyPanel.Controls.Add(pictureBox);
                    _historyPanel.Refresh();
                    Debug.WriteLine("Added to history panel directly");  
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in AddToHistory: {ex}");
        }
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            // Wait for map service to initialize
            _statusLabel.Text = "Initializing map...";
            await _mapViewerService.WaitForInitializationAsync();
            _statusLabel.Text = "Ready";

            // Restore compact UI if it was enabled
            if (_settingsService.CurrentSettings.CompactUIEnabled)
            {
                CompactUIButton_Click(null, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during form load: {ex}");
            MessageBox.Show(
                $"Error initializing map: {ex.Message}\nPlease ensure WebView2 Runtime is installed.",
                "Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsService.CurrentSettings;
        if (settings != null)
        {
            // Load capture settings
            _captureX.Value = Math.Max(_captureX.Minimum, Math.Min(_captureX.Maximum, settings.CaptureX));
            _captureY.Value = Math.Max(_captureY.Minimum, Math.Min(_captureY.Maximum, settings.CaptureY));
            _captureWidth.Value = Math.Max(_captureWidth.Minimum, Math.Min(_captureWidth.Maximum, settings.CaptureWidth));
            _captureHeight.Value = Math.Max(_captureHeight.Minimum, Math.Min(_captureHeight.Maximum, settings.CaptureHeight));
            _captureTimer.Interval = settings.CaptureInterval;
            _captureTimer.Interval = Math.Max(500, _settingsService.CurrentSettings.CaptureInterval); // Minimum 500ms

            // Load dark mode setting
            _isDarkMode = settings.IsDarkMode;

            // Load MainForm settings
            if (settings.MainFormSize.Width > 0 && settings.MainFormSize.Height > 0)
            {
                this.Size = settings.MainFormSize;
            }

            if (settings.MainFormLocation != Point.Empty)
            {
                var screen = Screen.FromPoint(settings.MainFormLocation);
                if (screen != null && screen.Bounds.Contains(settings.MainFormLocation))
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = settings.MainFormLocation;
                }
            }

            Debug.WriteLine($"Loaded settings: CaptureX={settings.CaptureX}, CaptureY={settings.CaptureY}, CaptureWidth={settings.CaptureWidth}, CaptureHeight={settings.CaptureHeight}");
            Debug.WriteLine($"Loaded MainForm settings: Size={this.Size}, Location={this.Location}, DarkMode={_isDarkMode}");

            // Restore Compact UI if it was enabled
            if (settings.CompactUIEnabled)
            {
                CompactUIButton_Click(null, EventArgs.Empty);
            }
        }

        // Apply dark mode based on loaded setting
        ApplyDarkMode(_isDarkMode);
    }



    private void SaveCurrentSettings()
    {
        var settings = _settingsService.CurrentSettings;

        // Save MainForm settings
        settings.MainFormSize = this.Size;
        settings.MainFormLocation = this.Location;

        _settingsService.SaveSettings(settings);
    }


    

    private void ShowHotkeys()
    {
        MessageBox.Show(
            "F9: Start/Stop Capture\n" +
            "Ctrl+F10: Single Capture\n" +
            "Ctrl+T: Test Capture Area\n" +
            "Ctrl+R: Reset All Arrows",
            "Hotkeys",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private void ResetPosition()
    {
        _captureX.Value = 0;
        _captureY.Value = 0;
        _captureWidth.Value = 200;
        _captureHeight.Value = 30;
        SaveCurrentSettings();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        SaveCurrentSettings();
        _hotKeyManager.Dispose();
        ClearCaptureHistory();
    }

    private void ClearCaptureHistory()
    {
        foreach (var pictureBox in _captureHistory)
        {
            pictureBox.Image?.Dispose();
            pictureBox.Dispose();
        }
        _captureHistory.Clear();
        _historyPanel.Controls.Clear();
    }
    private async Task CheckForUpdatesManually()
    {
        try
        {
            _statusLabel.Text = "Checking for updates...";

            var versionCheckService = new VersionCheckService(
                "Simplistik78",
                "OMNI",
                GetAppVersion.FromAboutDialog(),
                includePreReleases: true);  // Set to true to include pre-releases, Toggle setting in Program.cs too!

            bool updateFound = false;

            versionCheckService.UpdateAvailable += (s, e) => {
                updateFound = true;
                using var updateDialog = new UpdateNotificationDialog(
                    e.NewVersion,
                    e.ReleaseUrl,
                    e.ReleaseNotes);
                updateDialog.ShowDialog(this);
            };

            Debug.WriteLine("Manually checking for updates...");
            await versionCheckService.CheckForUpdatesAsync();
            Debug.WriteLine("Manual update check completed");

            // Update timestamp regardless of result
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

            if (!updateFound)
            {
                _statusLabel.Text = "Your application is up to date";
                MessageBox.Show(
                    $"You are using the latest version of OMNI (v{GetAppVersion.FromAboutDialog()}).",
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

    private void MainForm_Load(object sender, EventArgs e)
    {

    }
}