using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OMNI.Services.Update
{
    public class UpdateInstallerService
    {
        private readonly string _appDirectory;
        private readonly string _backupsDirectory;
        private readonly string _tempDirectory;
        private readonly string _currentVersion;
        private readonly string _appName = "OMNI";
        private string _lastError = string.Empty;

        public event EventHandler<string>? ProgressChanged;
        public event EventHandler<bool>? UpdateCompleted;
        public event EventHandler<UpdateErrorEventArgs>? UpdateError;

        public class VersionInfo
        {
            public string Version { get; set; } = string.Empty;
            public DateTime UpdatedOn { get; set; }
        }
        public UpdateInstallerService()
        {
            _appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _backupsDirectory = Path.Combine(_appDirectory, "Backups");
            _tempDirectory = Path.Combine(_appDirectory, "Temp");
            _currentVersion = VersionManagerService.GetCurrentVersion(); // Use new version service

            // Ensure directories exist
            try
            {
                Directory.CreateDirectory(_backupsDirectory);
                Directory.CreateDirectory(_tempDirectory);
                UpdateLoggerService.LogInfo($"Update service initialized. Current version: {_currentVersion}");
                UpdateLoggerService.LogInfo($"Backup directory: {_backupsDirectory}");
                UpdateLoggerService.LogInfo($"Temp directory: {_tempDirectory}");
            }
            catch (Exception ex)
            {
                UpdateLoggerService.LogError("Error initializing update directories", ex);
            }
        }


        /// <summary>
        /// Initiates the update process by downloading and installing the new version
        /// </summary>
        public async Task<bool> InstallUpdateAsync(string newVersion, string downloadUrl)
        {
            try
            {
                UpdateLoggerService.LogInfo($"Starting update from v{_currentVersion} to v{newVersion}");
                UpdateLoggerService.LogInfo($"Download URL: {downloadUrl}");
                OnProgressChanged($"Starting update from v{_currentVersion} to v{newVersion}");

                // Step 1: Download the update zip file
                var zipPath = await DownloadUpdateAsync(downloadUrl);
                if (string.IsNullOrEmpty(zipPath))
                {
                    string errorMsg = "Could not download update package";
                    _lastError = errorMsg;
                    UpdateLoggerService.LogError(errorMsg);
                    OnProgressChanged("Update failed: " + errorMsg);
                    OnUpdateError(errorMsg, null);
                    OnUpdateCompleted(false);
                    return false;
                }

                // Step 2: Create backup of current version
                var backupPath = await CreateBackupAsync();
                if (string.IsNullOrEmpty(backupPath))
                {
                    string errorMsg = "Could not create backup";
                    _lastError = errorMsg;
                    UpdateLoggerService.LogError(errorMsg);
                    OnProgressChanged("Update failed: " + errorMsg);
                    OnUpdateError(errorMsg, null);
                    OnUpdateCompleted(false);
                    return false;
                }

                // Step 3: Extract and install update
                var success = await ExtractAndInstallUpdateAsync(zipPath, newVersion);
                if (!success)
                {
                    string errorMsg = "Could not install update files";
                    _lastError = errorMsg;
                    UpdateLoggerService.LogError(errorMsg);
                    OnProgressChanged("Update failed: " + errorMsg);
                    OnUpdateError(errorMsg, null);
                    OnUpdateCompleted(false);
                    return false;
                }

                // Step 4: Clean up temp files
                CleanupTempFiles();

                UpdateLoggerService.LogInfo($"Update to v{newVersion} completed successfully");
                OnProgressChanged($"Update to v{newVersion} completed successfully");

                // Add final diagnostic logging
                var finalVersionDiagnosis = VersionManagerService.DiagnoseVersionSources();
                UpdateLoggerService.LogInfo("Final version diagnosis after update completion:");
                UpdateLoggerService.LogInfo(finalVersionDiagnosis);

                OnUpdateCompleted(true);
                return true;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                UpdateLoggerService.LogError("Update installation error", ex);
                OnProgressChanged($"Update failed: {ex.Message}");
                OnUpdateError("Update installation error", ex);
                OnUpdateCompleted(false);
                return false;
            }
        }
        public static string DiagnoseVersionSources()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Version Source Diagnosis:");

            // Check version.txt override file first
            try
            {
                var versionTxtPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt");
                sb.AppendLine($"Version.txt Path: {versionTxtPath}");
                sb.AppendLine($"Version.txt Exists: {File.Exists(versionTxtPath)}");

                if (File.Exists(versionTxtPath))
                {
                    var versionText = File.ReadAllText(versionTxtPath).Trim();
                    sb.AppendLine($"Version.txt Content: {versionText}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Version.txt Error: {ex.Message}");
            }

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

            // Check Entry Assembly Version (added for better diagnostics)
            try
            {
                var entryVersion = Assembly.GetEntryAssembly()?.GetName().Version;
                sb.AppendLine($"Entry Assembly Version: {(entryVersion != null ? $"{entryVersion.Major}.{entryVersion.Minor}.{entryVersion.Build}" : "NULL")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Entry Assembly Version Error: {ex.Message}");
            }

            // Check Version File
            try
            {
                var versionFilePath = VersionManagerService.GetVersionFilePath();
                sb.AppendLine($"Version File Path: {versionFilePath}");
                sb.AppendLine($"Version File Exists: {File.Exists(versionFilePath)}");

                if (File.Exists(versionFilePath))
                {
                    var json = File.ReadAllText(versionFilePath);
                    sb.AppendLine($"Version File Content: {json}");

                    try
                    {
                        var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);
                        sb.AppendLine($"Parsed Version: {(versionInfo != null ? versionInfo.Version : "NULL")}");
                        sb.AppendLine($"Updated On: {(versionInfo != null ? versionInfo.UpdatedOn.ToString() : "NULL")}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"Version File Parse Error: {ex.Message}");
                    }
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
        /// Downloads the update package from the specified URL
        /// </summary>
        private async Task<string> DownloadUpdateAsync(string downloadUrl)
        {
            try
            {
                UpdateLoggerService.LogInfo("Downloading update package...");
                OnProgressChanged("Downloading update package...");
                UpdateLoggerService.LogInfo($"Download URL: {downloadUrl}"); // Add this to log the exact URL

                string zipPath = Path.Combine(_tempDirectory, $"{_appName}_update.zip");

                using (var httpClient = new HttpClient())
                {
                    // Configure timeout and user agent
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "OMNI-Updater");

                    // Download with progress reporting
                    try
                    {
                        // First check if the URL exists with a HEAD request
                        var headRequest = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
                        var headResponse = await httpClient.SendAsync(headRequest);

                        if (!headResponse.IsSuccessStatusCode)
                        {
                            string errorMsg = $"URL validation failed: {headResponse.StatusCode} - {headResponse.ReasonPhrase}";
                            UpdateLoggerService.LogError(errorMsg);
                            _lastError = errorMsg;
                            return string.Empty;
                        }

                        // Then proceed with the download
                        using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                string errorMsg = $"HTTP error: {response.StatusCode} - {response.ReasonPhrase}";
                                UpdateLoggerService.LogError(errorMsg);
                                _lastError = errorMsg;
                                return string.Empty;
                            }

                            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                            UpdateLoggerService.LogInfo($"Download size: {(totalBytes > 0 ? $"{totalBytes / 1024.0 / 1024.0:F2} MB" : "Unknown")}");

                            var buffer = new byte[8192];
                            var bytesRead = 0;
                            var totalBytesRead = 0L;

                            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            {
                                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                                    totalBytesRead += bytesRead;
                                    if (totalBytes > 0)
                                    {
                                        var percentage = (int)(totalBytesRead * 100 / totalBytes);
                                        OnProgressChanged($"Downloading update: {percentage}% complete");
                                    }
                                }
                            }
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        string errorMsg = $"Download connection error: {ex.Message}";
                        UpdateLoggerService.LogError(errorMsg, ex);
                        _lastError = errorMsg;
                        return string.Empty;
                    }
                    catch (TaskCanceledException ex)
                    {
                        string errorMsg = "Download timed out. Please check your internet connection.";
                        UpdateLoggerService.LogError(errorMsg, ex);
                        _lastError = errorMsg;
                        return string.Empty;
                    }
                }

                // Verify the downloaded file
                if (!File.Exists(zipPath) || new FileInfo(zipPath).Length < 1000) // Basic size check
                {
                    string errorMsg = "Downloaded file is invalid or incomplete";
                    UpdateLoggerService.LogError(errorMsg);
                    _lastError = errorMsg;
                    return string.Empty;
                }

                UpdateLoggerService.LogInfo("Download completed successfully");
                OnProgressChanged("Download completed");
                return zipPath;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Download error: {ex.Message}";
                UpdateLoggerService.LogError(errorMsg, ex);
                _lastError = errorMsg;
                return string.Empty;
            }
        }

        /// <summary>
        /// Creates a backup of the current version
        /// </summary>
        private async Task<string> CreateBackupAsync()
        {
            try
            {
                UpdateLoggerService.LogInfo("Creating backup of current version...");
                OnProgressChanged("Creating backup of current version...");

                var backupFileName = $"{_appName}_v{_currentVersion}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                var backupPath = Path.Combine(_backupsDirectory, backupFileName);

                await Task.Run(() =>
                {
                    try
                    {
                        // Create a list of files to exclude from backup
                        var excludeDirectories = new[] { "Backups", "Temp", "Logs", "OMNI.exe.WebView2" };
                        int fileCount = 0;

                        using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                        {
                            foreach (var file in Directory.GetFiles(_appDirectory, "*", SearchOption.AllDirectories))
                            {
                                var relativePath = file.Substring(_appDirectory.Length);
                                var shouldExclude = false;

                                foreach (var dir in excludeDirectories)
                                {
                                    if (relativePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                                    {
                                        shouldExclude = true;
                                        break;
                                    }
                                }

                                if (!shouldExclude)
                                {
                                    archive.CreateEntryFromFile(file, relativePath.TrimStart('\\', '/'));
                                    fileCount++;
                                }
                            }
                        }

                        UpdateLoggerService.LogInfo($"Backup created with {fileCount} files");
                    }
                    catch (Exception ex)
                    {
                        UpdateLoggerService.LogError("Error creating backup archive", ex);
                        throw; // Re-throw to be caught by outer catch
                    }
                });

                // Verify backup was created successfully
                if (!File.Exists(backupPath) || new FileInfo(backupPath).Length < 1000) // Basic size check
                {
                    string errorMsg = "Created backup file is invalid or incomplete";
                    UpdateLoggerService.LogError(errorMsg);
                    _lastError = errorMsg;
                    return string.Empty;
                }

                UpdateLoggerService.LogInfo($"Backup created: {backupFileName}");
                OnProgressChanged($"Backup created: {backupFileName}");
                return backupPath;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Backup creation error: {ex.Message}";
                UpdateLoggerService.LogError(errorMsg, ex);
                _lastError = errorMsg;
                return string.Empty;
            }
        }

        /// <summary>
        /// Extracts and installs the update files
        /// </summary>
        private async Task<bool> ExtractAndInstallUpdateAsync(string zipPath, string newVersion)
        {
            try
            {
                UpdateLoggerService.LogInfo("Installing update...");
                OnProgressChanged("Installing update...");

                var extractPath = Path.Combine(_tempDirectory, "Extract");
                if (Directory.Exists(extractPath))
                {
                    try
                    {
                        Directory.Delete(extractPath, true);
                    }
                    catch (Exception ex)
                    {
                        UpdateLoggerService.LogWarning($"Could not clean up existing extract directory: {ex.Message}");
                    }
                }

                Directory.CreateDirectory(extractPath);

                // First, extract the update to temporary location
                try
                {
                    await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractPath, true));
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Failed to extract update package: {ex.Message}";
                    UpdateLoggerService.LogError(errorMsg, ex);
                    _lastError = errorMsg;
                    return false;
                }

                UpdateLoggerService.LogInfo("Update extracted, applying changes...");
                OnProgressChanged("Update extracted, applying changes...");

                // Get a list of running executables in the application directory
                var runningExecutables = new List<string>();
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.MainModule is not null)
                        {
                            var processPath = process.MainModule.FileName;
                            if (!string.IsNullOrEmpty(processPath) &&
                                processPath.StartsWith(_appDirectory, StringComparison.OrdinalIgnoreCase) &&
                                processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                runningExecutables.Add(Path.GetFileName(processPath));
                            }
                        }
                    }
                    catch { } // Ignore errors accessing process info
                }

                // Create a list of files that need to be updated after restart
                var pendingUpdates = new List<string>();
                var filesProcessed = 0;
                var filesCopied = 0;
                var filesPending = 0;

                // Apply the update files
                await Task.Run(() =>
                {
                    try
                    {
                        if (!Directory.Exists(extractPath))
                        {
                            throw new DirectoryNotFoundException($"Extract directory {extractPath} not found");
                        }

                        // Copy new and updated files from extracted directory to app directory
                        foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                        {
                            filesProcessed++;
                            var relativePath = file.Substring(extractPath.Length).TrimStart('\\', '/');
                            var targetPath = Path.Combine(_appDirectory, relativePath);

                            // Create target directory if it doesn't exist
                            var targetDir = Path.GetDirectoryName(targetPath);
                            if (!string.IsNullOrEmpty(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }

                            var fileName = Path.GetFileName(file);

                            // If the file is a running executable, mark it for update after restart
                            if (runningExecutables.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                            {
                                pendingUpdates.Add(relativePath);
                                filesPending++;
                                continue;
                            }

                            // Otherwise, copy the file now
                            try
                            {
                                // Set file attributes to normal in case file is read-only
                                if (File.Exists(targetPath))
                                {
                                    File.SetAttributes(targetPath, FileAttributes.Normal);
                                }

                                File.Copy(file, targetPath, true);
                                filesCopied++;
                            }
                            catch (Exception ex)
                            {
                                UpdateLoggerService.LogWarning($"Failed to copy file {relativePath}: {ex.Message}");
                                pendingUpdates.Add(relativePath ?? string.Empty);
                                filesPending++;
                            }
                        }

                        UpdateLoggerService.LogInfo($"Update install results: Processed {filesProcessed} files, " +
                                                   $"Copied {filesCopied} files, Pending {filesPending} files");
                    }
                    catch (Exception ex)
                    {
                        UpdateLoggerService.LogError("Error installing update files", ex);
                        throw; // Rethrow to be caught by outer catch
                    }
                });

                // If there are pending updates, write them to a file for post-restart processing
                if (pendingUpdates.Count > 0)
                {
                    var pendingPath = Path.Combine(_appDirectory, "pending_updates.json");
                    var pendingData = new
                    {
                        ExtractPath = extractPath,
                        Updates = pendingUpdates,
                        Version = newVersion
                    };
                    File.WriteAllText(pendingPath, System.Text.Json.JsonSerializer.Serialize(pendingData));

                    UpdateLoggerService.LogInfo($"Update partially applied. {pendingUpdates.Count} files will be updated after restart.");
                    OnProgressChanged($"Update partially applied. {pendingUpdates.Count} files will be updated after restart.");

                    // IMPORTANT FIX: DO NOT delete the extract directory if there are pending updates
                    // It needs to persist until the application restarts and can complete the pending updates
                    UpdateLoggerService.LogInfo("Preserving extraction directory for pending updates after restart");
                }
                else
                {
                    // Only delete extract directory if there are no pending updates
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                        UpdateLoggerService.LogInfo("Cleaned up extraction directory as no pending updates remain");
                    }

                    // CRITICAL FIX FOR v1.4.9 UPDATE ISSUES: 
                    // This creates a simple text file with just the version number that will be checked first
                    // in the version detection process, overriding any discrepancies between JSON and assembly versions.
                    // This is particularly important for users upgrading from v1.4.9 where the assembly version 
                    // might not update properly but we still need accurate version detection.
                    try
                    {
                        var versionTxtPath = Path.Combine(_appDirectory, "version.txt");
                        File.WriteAllText(versionTxtPath, newVersion);
                        UpdateLoggerService.LogInfo($"Created version.txt with version {newVersion} to fix version detection");
                    }
                    catch (Exception ex)
                    {
                        UpdateLoggerService.LogWarning($"Failed to create version.txt file: {ex.Message}");
                        // Continue with update even if this fails
                    }

                    // Diagnostic before updating version file
                    var versionBeforeUpdate = VersionManagerService.DiagnoseVersionSources();
                    UpdateLoggerService.LogInfo("Version diagnosis before update:");
                    UpdateLoggerService.LogInfo(versionBeforeUpdate);

                    // Update the version file with the new version
                    bool versionUpdateSuccess = VersionManagerService.UpdateVersionFile(newVersion);
                    UpdateLoggerService.LogInfo($"Version file update success: {versionUpdateSuccess}");

                    // Clear the version cache to force reload from file
                    VersionManagerService.ClearVersionCache();

                    // Diagnostic after updating version file
                    var versionAfterUpdate = VersionManagerService.DiagnoseVersionSources();
                    UpdateLoggerService.LogInfo("Version diagnosis after update:");
                    UpdateLoggerService.LogInfo(versionAfterUpdate);

                    UpdateLoggerService.LogInfo("All update files installed successfully.");
                    OnProgressChanged("All update files installed successfully.");
                }

                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Update installation error: {ex.Message}";
                UpdateLoggerService.LogError(errorMsg, ex);
                _lastError = errorMsg;
                return false;
            }
        }

        /// <summary>
        /// Checks for and completes any pending updates from a previous run
        /// </summary>
        public async Task<bool> CheckAndCompletePendingUpdatesAsync()
        {
            try
            {
                var pendingPath = Path.Combine(_appDirectory, "pending_updates.json");
                if (!File.Exists(pendingPath))
                {
                    return false; // No pending updates
                }

                UpdateLoggerService.LogInfo("Completing pending updates from previous installation...");

                try
                {
                    // Read the pending updates file
                    string pendingJson = await File.ReadAllTextAsync(pendingPath);

                    // Parse the JSON document
                    using (JsonDocument doc = JsonDocument.Parse(pendingJson))
                    {
                        JsonElement root = doc.RootElement;

                        // Get required properties
                        string? extractPath = null;
                        if (root.TryGetProperty("ExtractPath", out JsonElement extractElement))
                        {
                            extractPath = extractElement.GetString();
                        }

                        string? newVersion = null;
                        if (root.TryGetProperty("Version", out JsonElement versionElement))
                        {
                            newVersion = versionElement.GetString();
                        }

                        List<string> updates = new List<string>();
                        if (root.TryGetProperty("Updates", out JsonElement updatesElement) &&
                            updatesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement item in updatesElement.EnumerateArray())
                            {
                                string? path = item.GetString();
                                if (!string.IsNullOrEmpty(path))
                                {
                                    updates.Add(path);
                                }
                            }
                        }

                        // Check if the required information is available
                        if (string.IsNullOrEmpty(extractPath) || updates.Count == 0)
                        {
                            UpdateLoggerService.LogError("Pending update data is invalid or incomplete");
                            await Task.Run(() => File.Delete(pendingPath));
                            return false;
                        }

                        // Check if the extract directory exists
                        if (!Directory.Exists(extractPath))
                        {
                            UpdateLoggerService.LogError($"Extract directory not found: {extractPath}");
                            await Task.Run(() => File.Delete(pendingPath));
                            return false;
                        }

                        // Handle executable updates with a batch file
                        if (updates.Any(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Create a batch file to handle executable updates after the application exits
                            string batchPath = Path.Combine(_appDirectory, "complete_update.bat");

                            using (StreamWriter writer = new StreamWriter(batchPath))
                            {
                                writer.WriteLine("@echo off");
                                writer.WriteLine("echo OMNI Update - Completing installation...");
                                writer.WriteLine("echo Waiting for application to exit completely...");
                                writer.WriteLine("timeout /t 2 > nul");

                                // Find where OMNI.exe is in the extract folder - search recursively
                                writer.WriteLine("echo Locating updated files...");

                                // Search for OMNI.exe in the extract directory recursively
                                writer.WriteLine($"FOR /R \"{extractPath}\" %%F IN (OMNI.exe) DO (");
                                writer.WriteLine("  echo Found updated OMNI.exe: %%F");
                                writer.WriteLine($"  echo Copying to main application directory: {_appDirectory}");
                                writer.WriteLine($"  copy /Y \"%%F\" \"{Path.Combine(_appDirectory, "OMNI.exe")}\"");
                                writer.WriteLine(")");

                                // Also look for net8.0-windows folder specifically and copy its contents
                                writer.WriteLine($"IF EXIST \"{extractPath}\\net8.0-windows\" (");
                                writer.WriteLine($"  echo Found net8.0-windows folder, copying contents to main directory");
                                writer.WriteLine($"  xcopy \"{extractPath}\\net8.0-windows\\*.*\" \"{_appDirectory}\" /E /Y");
                                writer.WriteLine(")");

                                // Also look for Release\\net8.0-windows folder specifically
                                writer.WriteLine($"IF EXIST \"{extractPath}\\Release\\net8.0-windows\" (");
                                writer.WriteLine($"  echo Found Release\\net8.0-windows folder, copying contents to main directory");
                                writer.WriteLine($"  xcopy \"{extractPath}\\Release\\net8.0-windows\\*.*\" \"{_appDirectory}\" /E /Y");
                                writer.WriteLine(")");

                                // Create version.txt file to ensure correct version detection
                                if (!string.IsNullOrEmpty(newVersion))
                                {
                                    writer.WriteLine($"echo Creating version.txt with version {newVersion}...");
                                    writer.WriteLine($"echo {newVersion} > \"{Path.Combine(_appDirectory, "version.txt")}\"");
                                }

                                // Clean up
                                writer.WriteLine("echo Cleaning up temporary files...");
                                writer.WriteLine($"rmdir /S /Q \"{extractPath}\"");
                                writer.WriteLine($"del \"{pendingPath}\"");

                                // Start the application again
                                writer.WriteLine("echo Update completed successfully!");
                                writer.WriteLine($"start \"\" \"{Path.Combine(_appDirectory, "OMNI.exe")}\"");

                                // Self-delete the batch file
                                writer.WriteLine("echo Removing batch file...");
                                writer.WriteLine("del %0");
                            }

                            // Make the batch file run after this process exits
                            UpdateLoggerService.LogInfo("Created batch file to complete executable updates after restart");

                            // Start the batch file and then exit the application
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c \"{batchPath}\"",
                                WindowStyle = ProcessWindowStyle.Hidden,
                                CreateNoWindow = true,
                                UseShellExecute = true  // Use shell execute to avoid blocking
                            };

                            Process.Start(startInfo);

                            // Exit the application to allow the batch file to work
                            UpdateLoggerService.LogInfo("Exiting application to complete update...");
                            Environment.Exit(0);

                            // This will never be reached due to the Environment.Exit
                            return true;
                        }
                        else
                        {
                            // Handle non-executable updates
                            UpdateLoggerService.LogInfo("Processing non-executable pending updates...");

                            // Apply all pending updates
                            foreach (var relativePath in updates)
                            {
                                // Find the source file
                                string sourcePath = FindSourceFile(extractPath, relativePath);
                                if (string.IsNullOrEmpty(sourcePath))
                                {
                                    UpdateLoggerService.LogWarning($"Source file not found for: {relativePath}");
                                    continue;
                                }

                                // Determine the target path
                                string targetPath = DetermineTargetPath(_appDirectory, relativePath);

                                // Create target directory if needed
                                string? targetDir = Path.GetDirectoryName(targetPath);
                                if (!string.IsNullOrEmpty(targetDir))
                                {
                                    Directory.CreateDirectory(targetDir);
                                }

                                // Copy the file
                                if (File.Exists(targetPath))
                                {
                                    File.SetAttributes(targetPath, FileAttributes.Normal);
                                }

                                File.Copy(sourcePath, targetPath, true);
                                UpdateLoggerService.LogInfo($"Updated file: {Path.GetFileName(targetPath)}");
                            }

                            // Update version information
                            if (!string.IsNullOrEmpty(newVersion))
                            {
                                UpdateLoggerService.LogInfo($"Setting version to {newVersion}");

                                // Create version.txt file for reliable version detection
                                try
                                {
                                    string versionTxtPath = Path.Combine(_appDirectory, "version.txt");
                                    await File.WriteAllTextAsync(versionTxtPath, newVersion);
                                    UpdateLoggerService.LogInfo($"Created version.txt with version {newVersion}");
                                }
                                catch (Exception ex)
                                {
                                    UpdateLoggerService.LogWarning($"Failed to create version.txt: {ex.Message}");
                                }

                                // Update version file
                                await Task.Run(() => VersionManagerService.UpdateVersionFile(newVersion));
                                VersionManagerService.ClearVersionCache();
                            }

                            // Clean up
                            await Task.Run(() => File.Delete(pendingPath));
                            await Task.Run(() => Directory.Delete(extractPath, true));

                            UpdateLoggerService.LogInfo("Pending updates completed successfully");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateLoggerService.LogError("Error processing pending updates", ex);
                    try { await Task.Run(() => File.Delete(pendingPath)); } catch { }
                    return false;
                }
            }
            catch (Exception ex)
            {
                UpdateLoggerService.LogError("Error checking for pending updates", ex);
                return false;
            }
        }

        // Helper methods for finding files
        private string FindSourceFile(string extractPath, string relativePath)
        {
            // Check direct path
            string directPath = Path.Combine(extractPath, relativePath);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            // Check without "Release\" prefix
            if (relativePath.StartsWith("Release\\"))
            {
                string noReleasePath = Path.Combine(extractPath, relativePath.Substring("Release\\".Length));
                if (File.Exists(noReleasePath))
                {
                    return noReleasePath;
                }
            }

            // Look for OMNI.exe in the net8.0-windows folder
            if (relativePath.EndsWith("OMNI.exe", StringComparison.OrdinalIgnoreCase))
            {
                string netFolderPath = Path.Combine(extractPath, "net8.0-windows", "OMNI.exe");
                if (File.Exists(netFolderPath))
                {
                    return netFolderPath;
                }

                string releaseNetFolderPath = Path.Combine(extractPath, "Release", "net8.0-windows", "OMNI.exe");
                if (File.Exists(releaseNetFolderPath))
                {
                    return releaseNetFolderPath;
                }
            }

            // Search recursively as a last resort
            foreach (var file in Directory.GetFiles(extractPath, Path.GetFileName(relativePath), SearchOption.AllDirectories))
            {
                return file; // Return the first match
            }

            return string.Empty; // Not found
        }

        private string DetermineTargetPath(string appDirectory, string relativePath)
        {
            // For OMNI.exe, always put it in the main application directory
            if (relativePath.EndsWith("OMNI.exe", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(appDirectory, "OMNI.exe");
            }

            // Remove "Release\" prefix if present
            string targetRelativePath = relativePath;
            if (relativePath.StartsWith("Release\\"))
            {
                targetRelativePath = relativePath.Substring("Release\\".Length);
            }

            // Return the final target path
            return Path.Combine(appDirectory, targetRelativePath);
        }

        /// <summary>
        /// Reverts to a specific backup version
        /// </summary>
        public async Task<bool> RollbackToVersionAsync(string backupFileName)
        {
            try
            {
                var backupPath = Path.Combine(_backupsDirectory, backupFileName);
                if (!File.Exists(backupPath))
                {
                    string errorMsg = $"Backup file not found: {backupFileName}";
                    UpdateLoggerService.LogError(errorMsg);
                    OnProgressChanged(errorMsg);
                    return false;
                }

                UpdateLoggerService.LogInfo($"Rolling back to version from backup: {backupFileName}");
                OnProgressChanged($"Rolling back to version from backup: {backupFileName}");

                // Create a backup of current version before rollback
                await CreateBackupAsync();

                // Extract the backup to a temporary location
                var extractPath = Path.Combine(_tempDirectory, "Rollback");
                if (Directory.Exists(extractPath))
                {
                    try
                    {
                        Directory.Delete(extractPath, true);
                    }
                    catch (Exception ex)
                    {
                        UpdateLoggerService.LogWarning($"Could not clean up existing rollback directory: {ex.Message}");
                    }
                }

                Directory.CreateDirectory(extractPath);

                await Task.Run(() => ZipFile.ExtractToDirectory(backupPath, extractPath, true));

                // Handle running executables similar to update process
                var runningExecutables = new List<string>();
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.MainModule is not null)
                        {
                            var processPath = process.MainModule.FileName;
                            if (!string.IsNullOrEmpty(processPath) &&
                                processPath.StartsWith(_appDirectory, StringComparison.OrdinalIgnoreCase) &&
                                processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                runningExecutables.Add(Path.GetFileName(processPath));
                            }
                        }
                    }
                    catch { } // Ignore errors accessing process info
                }

                var pendingUpdates = new List<string>();

                // Apply the rollback files
                await Task.Run(() =>
                {
                    foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = file.Substring(extractPath.Length).TrimStart('\\', '/');
                        var targetPath = Path.Combine(_appDirectory, relativePath);

                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        var fileName = Path.GetFileName(file);

                        if (runningExecutables.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                        {
                            pendingUpdates.Add(relativePath);
                            continue;
                        }

                        try
                        {
                            if (File.Exists(targetPath))
                            {
                                File.SetAttributes(targetPath, FileAttributes.Normal);
                            }
                            File.Copy(file, targetPath, true);
                        }
                        catch
                        {
                            pendingUpdates.Add(relativePath);
                        }
                    }
                });

                // Handle pending updates
                if (pendingUpdates.Count > 0)
                {
                    var pendingPath = Path.Combine(_appDirectory, "pending_rollback.json");
                    var pendingData = new
                    {
                        ExtractPath = extractPath,
                        Updates = pendingUpdates
                    };
                    File.WriteAllText(pendingPath, JsonSerializer.Serialize(pendingData));

                    UpdateLoggerService.LogInfo($"Rollback partially applied. {pendingUpdates.Count} files will be updated after restart.");
                    OnProgressChanged($"Rollback partially applied. {pendingUpdates.Count} files will be updated after restart.");
                }
                else
                {
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }
                    UpdateLoggerService.LogInfo("Rollback completed successfully.");
                    OnProgressChanged("Rollback completed successfully.");
                }

                return true;
            }
            catch (Exception ex)
            {
                UpdateLoggerService.LogError("Rollback error", ex);
                OnUpdateError($"Rollback failed: {ex.Message}", ex);
                OnProgressChanged($"Rollback failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns a list of available backups
        /// </summary>
        public List<BackupInfo> GetAvailableBackups()
        {
            var backups = new List<BackupInfo>();

            try
            {
                if (!Directory.Exists(_backupsDirectory))
                {
                    UpdateLoggerService.LogWarning($"Backups directory not found: {_backupsDirectory}");
                    return backups;
                }

                foreach (var file in Directory.GetFiles(_backupsDirectory, $"{_appName}_v*.zip"))
                {
                    var fileName = Path.GetFileName(file);
                    var fileInfo = new FileInfo(file);

                    // Parse version from filename (format: OMNI_v1.2.3_20240325_123456.zip)
                    var parts = fileName.Split('_');
                    if (parts.Length >= 2 && parts[0] == _appName)
                    {
                        var version = parts[1].TrimStart('v');
                        var timestamp = fileInfo.CreationTime;

                        backups.Add(new BackupInfo
                        {
                            FileName = fileName,
                            Version = version,
                            CreationDate = timestamp,
                            FileSizeInMB = Math.Round((double)fileInfo.Length / (1024 * 1024), 2)
                        });
                    }
                }

                // Sort by creation date, newest first
                return backups.OrderByDescending(b => b.CreationDate).ToList();
            }
            catch (Exception ex)
            {
                UpdateLoggerService.LogError("Error getting available backups", ex);
                return backups;
            }
        }

        /// <summary>
        /// Cleans up temporary files used during update
        /// </summary>
        private void CleanupTempFiles()
        {
            try
            {
                UpdateLoggerService.LogInfo("Cleaning up temporary files...");

                // Check if there are pending updates
                var pendingPath = Path.Combine(_appDirectory, "pending_updates.json");
                bool hasPendingUpdates = File.Exists(pendingPath);

                foreach (var file in Directory.GetFiles(_tempDirectory, "*.zip"))
                {
                    File.Delete(file);
                }

                // Only clean up directories that are not the Extract directory if we have pending updates
                foreach (var dir in Directory.GetDirectories(_tempDirectory))
                {
                    if (hasPendingUpdates &&
                        Path.GetFileName(dir).Equals("Extract", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip the Extract directory if we have pending updates
                        UpdateLoggerService.LogInfo("Skipping Extract directory cleanup due to pending updates");
                        continue;
                    }

                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }

                UpdateLoggerService.LogInfo("Temporary files cleaned up");
            }
            catch (Exception ex)
            {
                UpdateLoggerService.LogError("Error cleaning up temp files", ex);
            }
        }

        /// <summary>
        /// Performs backup cleanup based on retention policy
        /// </summary>
        public void CleanupOldBackups(int maxBackupsToKeep = 5)
        {
            try
            {
                var backups = GetAvailableBackups();

                // Keep the specified number of most recent backups
                if (backups.Count > maxBackupsToKeep)
                {
                    foreach (var backup in backups.Skip(maxBackupsToKeep))
                    {
                        var path = Path.Combine(_backupsDirectory, backup.FileName);
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            UpdateLoggerService.LogInfo($"Deleted old backup: {backup.FileName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateLoggerService.LogError("Error cleaning up old backups", ex);
            }
        }

        /// <summary>
        /// Gets the last error message
        /// </summary>
        public string GetLastError()
        {
            return _lastError;
        }

        /// <summary>
        /// Gets the path to the log file
        /// </summary>
        public string GetLogFilePath()
        {
            return UpdateLoggerService.GetLogFilePath();
        }

        /// <summary>
        /// Gets the current session log content
        /// </summary>
        public string GetLogContent()
        {
            return UpdateLoggerService.GetCurrentSessionLog();
        }

        private void OnProgressChanged(string message)
        {
            Debug.WriteLine($"Update progress: {message}");
            UpdateLoggerService.LogInfo(message);
            ProgressChanged?.Invoke(this, message);
        }

        private void OnUpdateCompleted(bool success)
        {
            UpdateLoggerService.LogInfo($"Update completed with status: {(success ? "Success" : "Failed")}");
            UpdateCompleted?.Invoke(this, success);
        }

        private void OnUpdateError(string message, Exception? ex)
        {
            UpdateLoggerService.LogError(message, ex);
            UpdateError?.Invoke(this, new UpdateErrorEventArgs(message, ex, UpdateLoggerService.GetLogFilePath()));
        }

    }

    public class UpdateErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception? Exception { get; }
        public string LogFilePath { get; }

        public UpdateErrorEventArgs(string message, Exception? exception, string logFilePath)
        {
            Message = message;
            Exception = exception;
            LogFilePath = logFilePath;
        }
    }

}