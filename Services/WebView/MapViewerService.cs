using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OMNI.Services.WebView
{
    public class MapViewerService : IMapViewerService, IDisposable
    {
        private readonly WebView2 _webView;
        private readonly bool _isCompactMode;
        private bool _isInitialized;
        private bool _isInitializing;
        private readonly TaskCompletionSource<bool> _initializationTcs = new();
        private bool _disposed;
        private bool _isMapReady;
        private readonly Queue<(float x, float y, float heading)> _pendingMarkers = new();
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _keepHistory;
        private static CoreWebView2Environment? _sharedEnvironment;
        private static readonly SemaphoreSlim _environmentLock = new(1, 1);
        private EventHandler<CoreWebView2NavigationCompletedEventArgs>? _navigationCompletedHandler;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;

        public MapViewerService(WebView2 webView, bool isCompactMode = false)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _isCompactMode = isCompactMode;

            // Initialize the navigation completed handler
            _navigationCompletedHandler = (s, e) =>
            {
                if (e.IsSuccess)
                {
                    _isMapReady = true;
                    OnStatusChanged("Map initialized successfully");
                    _ = ProcessPendingMarkersAsync();
                }
                else
                {
                    var error = new Exception($"Navigation failed: {e.WebErrorStatus}");
                    OnErrorOccurred(error);
                }
            };

            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;

                _isInitialized = true;
                _initializationTcs.TrySetResult(true);
                ConfigureWebView();
            }
            else
            {
                _ = InitializeWebView2Async();
            }
        }
        public async Task WaitForInitializationAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _initializationTcs.Task;
        }

        private static async Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync()
        {
            await _environmentLock.WaitAsync();
            try
            {
                if (_sharedEnvironment == null)
                {
                    var options = new CoreWebView2EnvironmentOptions
                    {
                        AdditionalBrowserArguments = "--disable-web-security --allow-file-access-from-files"
                    };

                    _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, null, options);
                }
                return _sharedEnvironment;
            }
            finally
            {
                _environmentLock.Release();
            }
        }

        private string GetInitScript()
        {
            string compactModeCss = _isCompactMode ? @"
/* Remove the entire top section */
nav,
header,
.navbar,
.navbar *,
[class*=""navbar""],
[class*=""header""],
[class*=""brand""],
[class*=""logo""],
[id*=""header""],
[id*=""navbar""],
[id*=""brand""],
[id*=""logo""],
.menu-toggle,
.toggle-nav,
.site-header,
.site-branding,
.nav-wrapper,
.navbar-toggle,
.menu-button {
    display: none !important;
    visibility: hidden !important;
    opacity: 0 !important;
    height: 0 !important;
    width: 0 !important;
    padding: 0 !important;
    margin: 0 !important;
    border: none !important;
    position: absolute !important;
    pointer-events: none !important;
    clip: rect(0, 0, 0, 0) !important;
    overflow: hidden !important;
}

/* Remove any gaps at the top */
body > *:first-child {
    margin-top: 0 !important;
    padding-top: 0 !important;
}

/* Force map to full viewport */
#map {
    position: fixed !important;
    top: 0 !important;
    left: 0 !important;
    right: 0 !important;
    bottom: 0 !important;
    width: 100vw !important;
    height: 100vh !important;
    margin: 0 !important;
    padding: 0 !important;
    z-index: 1 !important;
}

/* Fix container sizing */
.map-container,
.content-area,
.site-content,
.main-content,
body,
html {
    margin: 0 !important;
    padding: 0 !important;
    height: 100% !important;
    width: 100% !important;
    overflow: hidden !important;
}

/* Ensure map controls stay visible */
.leaflet-control-container {
    display: block !important;
    visibility: visible !important;
    z-index: 1000 !important;
}

.leaflet-control-zoom {
    display: block !important;
    visibility: visible !important;
}

/* Hide any elements with background colors that might be part of the header */
[style*=""background""] {
    background: none !important;
}

/* Remove any fixed positioning that might create space */
* {
    position: static;
}

#map {
    position: fixed !important;
}

/* Hide pin drop controls and related elements */
.drop-pin-controls,
.drop-pin-button,
.pin-controls,
.pin-input,
[class*=""pin-""],
[id*=""pin-""],
[class*=""drop-""],
[id*=""drop-""],
.coordinate-input,
.location-input,
#dropPinAtLocBtn,
.input-group,
.control-group {
    visibility: hidden !important;
    opacity: 0 !important;
    position: absolute !important;
    left: -9999px !important;
    top: -9999px !important;
    pointer-events: none !important;
    height: 0 !important;
    width: 0 !important;
    margin: 0 !important;
    padding: 0 !important;
    border: none !important;
    display: block !important; /* Keep display:block so the elements work but are invisible */
}

/* Ensure inputs still function but are invisible */
.drop-pin-controls input,
.pin-controls input {
    position: absolute !important;
    left: -9999px !important;
    top: -9999px !important;
    opacity: 0 !important;
    pointer-events: none !important;
}
" : string.Empty;

            return @"
if (window.markerSystemInitialized) {
    console.log('Marker system already initialized');
    return;
}

window.markerSystemInitialized = true;
console.log('Starting marker system initialization');

function initializeSystem() {
    // Add custom CSS
    var customCSS = document.createElement('style');
    customCSS.type = 'text/css';
    customCSS.innerHTML = `
        .custom-arrow-icon {
            background: none !important;
            border: none !important;
            box-shadow: none !important;
            background-image: none !important;
            width: 20px !important;
            height: 20px !important;
            opacity: 1 !important;
        }
        " + compactModeCss + @"
    `;
    document.head.appendChild(customCSS);

    function getCoordinates(x, y) {
        return new Promise((resolve) => {
            // Click pin drop to initialize coordinate system
            const dropPinBtn = document.querySelector('.drop-pin-controls button');
            if (!dropPinBtn) {
                console.error('Drop-pin button not found');
                resolve(null);
                return;
            }

            // Click quietly in the background
            dropPinBtn.click();

            // Wait for inputs
            let attempts = 0;
            const maxAttempts = 20;

            function checkInputs() {
                const inputs = document.querySelectorAll('.drop-pin-controls input[type=""text""]');
                if (inputs.length >= 2) {
                    // Fill coordinates
                    inputs[0].value = x;
                    inputs[1].value = y;

                    // Click drop button
                    const dropBtn = document.querySelector('.drop-pin-controls button#dropPinAtLocBtn');
                    if (dropBtn) {
                        dropBtn.click();

                        // Get position and immediately cleanup
                        setTimeout(() => {
                            let targetPin = null;
                            window.ShalazamLeafletMap.eachLayer((layer) => {
                                if (layer instanceof L.Marker) {
                                    targetPin = layer;
                                    window.ShalazamLeafletMap.removeLayer(layer);
                                }
                            });

                            if (targetPin) {
                                resolve(targetPin.getLatLng());
                            } else {
                                resolve(null);
                            }
                        }, 50);
                    } else {
                        resolve(null);
                    }
                } else if (attempts++ < maxAttempts) {
                    setTimeout(checkInputs, 50);
                } else {
                    resolve(null);
                }
            }

            checkInputs();
        });
    }

    function initGameMarker() {
        if (!window.ShalazamLeafletMap) {
            console.error('Map instance not found. Waiting for map instance...');
            setTimeout(initGameMarker, 500);
            return;
        }
        console.log('Map instance found.');

        window.gameMarker = {
            current: null,
            lastCoords: null,
            add: async function(x, y, heading) {
                try {
                    // Check if coordinates are the same
                    const currentCoords = `${x},${y},${heading}`;
                    if (this.lastCoords === currentCoords) {
                        return 'Coordinates unchanged';
                    }
                    this.lastCoords = currentCoords;

                    // Get correct map position
                    const latlng = await getCoordinates(x, y);
                    if (!latlng) {
                        return 'Error: Could not get map position';
                    }

                    // Remove existing marker
                    if (this.current) {
                        this.current.remove();
                        this.current = null;
                    }

                    // Create arrow icon
                    const arrowIcon = L.divIcon({
    html: `<svg xmlns=""http://www.w3.org/2000/svg"" width=""25"" height=""25""
               viewBox=""0 0 20 20"" style=""transform: rotate(${heading}deg);"">
               <path d=""M10 2 L16 16 L10 12 L4 16 Z"" fill=""#00FFFF"" stroke=""black"" stroke-width=""2""/>
           </svg>`,
    className: 'custom-arrow-icon',
    iconSize: [25, 25],
    iconAnchor: [15, 15]
});

                    // Add marker
                    this.current = L.marker(latlng, {
                        icon: arrowIcon,
                        zIndexOffset: 1000,
                        interactive: false
                    }).addTo(window.ShalazamLeafletMap);

                    // Center map
                    window.ShalazamLeafletMap.setView(latlng, window.ShalazamLeafletMap.getZoom());
                    console.log('Arrow placed at:', latlng, 'with heading:', heading);
                    return 'Marker added successfully';
                } catch (e) {
                    console.error('Error in add marker function:', e);
                    return 'Error: ' + e.message;
                }
            },
            clear: function() {
                if (this.current) {
                    this.current.remove();
                    this.current = null;
                }
                this.lastCoords = null;
                return 'Marker cleared';
            }
        };

        console.log('gameMarker object initialized.');
        window.mapHandler = window.gameMarker;
    }

    // Start initialization
    initGameMarker();
}

if (document.readyState === 'loading') {
    console.log('Document still loading, waiting...');
    document.addEventListener('DOMContentLoaded', initializeSystem);
} else {
    console.log('Document ready, initializing...');
    initializeSystem();
}";
        }


        private async Task InitializeWebView2Async()
        {
            if (_disposed || _webView.IsDisposed) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized || _isInitializing) return;
                _isInitializing = true;

                Debug.WriteLine("Starting WebView2 initialization");
                OnStatusChanged("Initializing WebView2...");

                try
                {
                    var environment = await GetOrCreateEnvironmentAsync();

                    if (_disposed || _webView.IsDisposed) return;

                    if (_webView.CoreWebView2 == null)
                    {
                        await _webView.EnsureCoreWebView2Async(environment);
                    }

                    ConfigureWebView();

                    _isInitialized = true;
                    _initializationTcs.TrySetResult(true);
                    OnStatusChanged("WebView2 initialization complete");
                }
                catch (Exception ex)
                {
                    var message = "Failed to initialize WebView2. Please ensure WebView2 Runtime is installed.";
                    Debug.WriteLine($"{message}: {ex}");
                    _initializationTcs.TrySetException(new InvalidOperationException(message, ex));
                    OnErrorOccurred(ex);
                    throw;
                }
            }
            finally
            {
                _isInitializing = false;
                _initLock.Release();
            }
        }

        private void ConfigureWebView()
        {
            if (_webView.CoreWebView2 == null || _disposed) return;

            _webView.CoreWebView2.Settings.IsScriptEnabled = true;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            // Add initial script
            _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetInitScript());

            _webView.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                try
                {
                    if (e.IsSuccess)
                    {
                        // Wait for the page to stabilize
                        await Task.Delay(1000);

                        // Ensure map and marker system are initialized
                        string initScript = @"
                    (async function() {
                        if (window.markerSystemInitialized) {
                            return 'Already initialized';
                        }
                        await new Promise(resolve => setTimeout(resolve, 500));
                        
                        if (!window.ShalazamLeafletMap) {
                            return 'Map not ready';
                        }

                        if (!window.gameMarker) {
                            " + GetInitScript() + @"
                            return 'Initialized marker system';
                        }
                        return 'System ready';
                    })()";

                        var result = await _webView.CoreWebView2.ExecuteScriptAsync(initScript);
                        Debug.WriteLine($"Map initialization result: {result}");
                        OnStatusChanged("Map initialized successfully");
                    }
                    else
                    {
                        var error = new Exception($"Navigation failed: {e.WebErrorStatus}");
                        OnErrorOccurred(error);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in navigation completed handler: {ex}");
                    OnErrorOccurred(ex);
                }
            };

            _webView.CoreWebView2.Navigate("https://shalazam.info/maps/1");
        }

        public async Task<string> AddMarkerAsync(float x, float y, float heading)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_isInitialized)
            {
                try
                {
                    await WaitForInitializationAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize WebView2: {ex}");
                    return "Error: WebView2 initialization failed";
                }
            }

            try
            {
                // Format numbers using invariant culture to ensure consistent decimal separators
                string xStr = x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                string yStr = y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                string headingStr = heading.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

                Debug.WriteLine($"Adding marker with invariant culture format: X={xStr}, Y={yStr}, H={headingStr}");

                // Check and reinitialize if needed
                string checkScript = @"
            (async function() {
                if (!window.ShalazamLeafletMap || !window.gameMarker) {
                    // Reinitialize
                    " + GetInitScript() + @"
                    // Wait for initialization
                    await new Promise(resolve => setTimeout(resolve, 1000));
                    
                    if (!window.ShalazamLeafletMap || !window.gameMarker) {
                        return 'not_ready';
                    }
                }
                return 'ready';
            })()";

                var checkResult = await _webView.CoreWebView2.ExecuteScriptAsync(checkScript);
                if (checkResult.Contains("not_ready"))
                {
                    Debug.WriteLine("Map or marker system not ready");
                    return "Error: Map system not ready";
                }

                // Add the marker using the invariant culture formatted strings
                string script = $@"
            (async function() {{
                try {{
                    await new Promise(resolve => setTimeout(resolve, 500));
                    return window.gameMarker.add({xStr}, {yStr}, {headingStr});
                }} catch (error) {{
                    console.error('Error in marker addition:', error);
                    return 'Error: ' + error.message;
                }}
            }})()";

                var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                Debug.WriteLine($"Marker addition result: {result}");
                return result.Trim('"');
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding marker: {ex}");
                OnErrorOccurred(ex);
                return $"Error: {ex.Message}";
            }
        }


        public async Task<string> ClearMarkersAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_isInitialized)
            {
                return "Error: WebView2 not initialized";
            }

            try
            {
                const string script = @"
                    (function() {
                        try {
                            if (!window.mapHandler) {
                                return 'Map handler not available';
                            }
                            return window.mapHandler.clear();
                        } catch (error) {
                            console.error('Error clearing markers:', error);
                            return 'Error: ' + error.message;
                        }
                    })()";

                var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                Debug.WriteLine($"Clear markers result: {result}");
                return result.Trim('"');
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing markers: {ex}");
                OnErrorOccurred(ex);
                return $"Error: {ex.Message}";
            }
        }

        public async Task SetKeepHistoryAsync(bool keepHistory)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _keepHistory = keepHistory;

            if (_isInitialized && _isMapReady)
            {
                try
                {
                    var script = $"window.mapHandler.keepHistory = {keepHistory.ToString().ToLower()};";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    Debug.WriteLine($"Set keep history to: {keepHistory}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting keep history: {ex}");
                    OnErrorOccurred(ex);
                }
            }
        }

        private async Task ProcessPendingMarkersAsync()
        {
            while (_pendingMarkers.Count > 0)
            {
                try
                {
                    var (x, y, heading) = _pendingMarkers.Dequeue();
                    var result = await AddMarkerAsync(x, y, heading);
                    Debug.WriteLine($"Processed pending marker: {result}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing pending marker: {ex}");
                    OnErrorOccurred(ex);
                }
            }
        }

        protected virtual void OnStatusChanged(string status)
        {
            try
            {
                StatusChanged?.Invoke(this, status);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnStatusChanged: {ex}");
            }
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            try
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            catch (Exception innerEx)
            {
                Debug.WriteLine($"Error in OnErrorOccurred: {innerEx}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _isInitialized = false;
                _initializationTcs.TrySetCanceled();
                _initLock.Dispose();
                _pendingMarkers.Clear();

                try
                {
                    if (_webView.CoreWebView2 != null && _navigationCompletedHandler != null)
                    {
                        _webView.CoreWebView2.NavigationCompleted -= _navigationCompletedHandler;
                        _webView.CoreWebView2.Settings.IsScriptEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during disposal: {ex}");
                }
            }
        }
    }
}