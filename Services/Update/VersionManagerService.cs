using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics;
using OMNI.Services.Update;

namespace OMNI.Services
{
    public class VersionManagerService
    {
        private const string VERSION_FILE = "app_version.json";
        private static string? _cachedVersion = null;

        /// <summary>
        /// Gets the current application version from multiple sources in priority order:
        /// 1. From embedded assembly version
        /// 2. From version file if it exists
        /// 3. From AboutDialog as fallback (legacy method)
        /// </summary>
        public static string GetCurrentVersion()
        {
            // Use cached version if available
            if (!string.IsNullOrEmpty(_cachedVersion))
            {
                return _cachedVersion;
            }

            // 1. Try to get version from version file FIRST (highest priority)
            try
            {
                var versionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VERSION_FILE);
                if (File.Exists(versionFilePath))
                {
                    var json = File.ReadAllText(versionFilePath);
                    var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);
                    if (versionInfo != null && !string.IsNullOrEmpty(versionInfo.Version))
                    {
                        _cachedVersion = versionInfo.Version;
                        Debug.WriteLine($"Using version from file: {_cachedVersion}");
                        return _cachedVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading version file: {ex.Message}");
            }

            // 2. Try to get version from Entry Assembly (the main EXE) instead of calling assembly
            try
            {
                // This gets the main application assembly, not the calling library
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                if (version != null)
                {
                    _cachedVersion = $"{version.Major}.{version.Minor}.{version.Build}";
                    Debug.WriteLine($"Using version from entry assembly: {_cachedVersion}");
                    return _cachedVersion;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting entry assembly version: {ex.Message}");
            }

            // 3. Fallback to AboutDialog method as last resort
            _cachedVersion = GetAppVersion.FromAboutDialog();
            Debug.WriteLine($"Using version from AboutDialog: {_cachedVersion}");
            return _cachedVersion;
        }
        public static string GetVersionFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VERSION_FILE);
        }
        /// <summary>
        /// Updates the version file with the new version after a successful update
        /// </summary>
        public static bool UpdateVersionFile(string newVersion)
        {
            try
            {
                var versionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VERSION_FILE);
                var versionInfo = new VersionInfo
                {
                    Version = newVersion,
                    UpdatedOn = DateTime.Now
                };

                var json = JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(versionFilePath, json);
                _cachedVersion = newVersion; // Update cached version

                // Add more detailed logging
                UpdateLoggerService.LogInfo($"Version file updated at: {versionFilePath}");
                UpdateLoggerService.LogInfo($"Version file content: {json}");
                Debug.WriteLine($"Version file updated to: {newVersion} at {versionFilePath}");

                return true;
            }
            catch (Exception ex)
            {
                UpdateLoggerService.LogError($"Error updating version file: {ex.Message}", ex);
                Debug.WriteLine($"Error updating version file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clears the cached version to force reload from source
        /// </summary>
        public static void ClearVersionCache()
        {
            _cachedVersion = null;
            UpdateLoggerService.LogInfo("Version cache cleared");
        }

        /// <summary>
        /// Diagnostic method to check all version sources
        /// </summary>
        public static string DiagnoseVersionSources()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Version Source Diagnosis:");

            // Check Version File first (highest priority)
            try
            {
                var versionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VERSION_FILE);
                sb.AppendLine($"Version File Path: {versionFilePath}");
                sb.AppendLine($"Version File Exists: {File.Exists(versionFilePath)}");

                if (File.Exists(versionFilePath))
                {
                    var json = File.ReadAllText(versionFilePath);
                    sb.AppendLine($"Version File Content: {json}");

                    var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);
                    sb.AppendLine($"Parsed Version: {(versionInfo != null ? versionInfo.Version : "NULL")}");
                    sb.AppendLine($"Updated On: {(versionInfo != null ? versionInfo.UpdatedOn.ToString() : "NULL")}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Version File Error: {ex.Message}");
            }

            // Check Entry Assembly Version
            try
            {
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                sb.AppendLine($"Entry Assembly Version: {(version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "NULL")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Entry Assembly Version Error: {ex.Message}");
            }

            // Check Executing Assembly Version
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                sb.AppendLine($"Executing Assembly Version: {(version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "NULL")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Executing Assembly Version Error: {ex.Message}");
            }

            // Check AboutDialog Version
            try
            {
                var aboutVersion = GetAppVersion.FromAboutDialog();
                sb.AppendLine($"AboutDialog Version: {aboutVersion}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"AboutDialog Version Error: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Class representing version information stored in the JSON file
        /// </summary>
        private class VersionInfo
        {
            public string Version { get; set; } = string.Empty;
            public DateTime UpdatedOn { get; set; }
        }
    }
}