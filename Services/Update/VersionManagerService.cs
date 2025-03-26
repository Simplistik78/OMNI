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

            // Try to get version from Assembly first
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    _cachedVersion = $"{version.Major}.{version.Minor}.{version.Build}";
                    return _cachedVersion;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting assembly version: {ex.Message}");
            }

            // Try to get version from version file
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
                        return _cachedVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading version file: {ex.Message}");
            }

            // Fallback to AboutDialog method (legacy)
            _cachedVersion = GetAppVersion.FromAboutDialog();
            return _cachedVersion;
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

            // Check Assembly Version
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                sb.AppendLine($"Assembly Version: {(version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "NULL")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Assembly Version Error: {ex.Message}");
            }

            // Check Version File
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