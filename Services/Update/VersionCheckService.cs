using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace OMNI.Services.Update
{
    public class VersionCheckService
    {
        private readonly string _repoOwner;
        private readonly string _repoName;
        private readonly string _currentVersion;
        private readonly HttpClient _httpClient;
        private readonly bool _includePreReleases;

        public event EventHandler<UpdateAvailableEventArgs>? UpdateAvailable;

        public VersionCheckService(string repoOwner, string repoName, bool includePreReleases = false)
        {
            _repoOwner = repoOwner ?? throw new ArgumentNullException(nameof(repoOwner));
            _repoName = repoName ?? throw new ArgumentNullException(nameof(repoName));
            _currentVersion = VersionManagerService.GetCurrentVersion(); // Use the new version service
            _includePreReleases = includePreReleases;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OMNI App");
        }


        public async Task CheckForUpdatesAsync()
        {
            try
            {
                Debug.WriteLine($"Checking for updates. Current version: {_currentVersion}");

                // Choose URL based on whether we want pre-releases or just stable releases
                string url = _includePreReleases
                    ? $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases"
                    : $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";

                Debug.WriteLine($"GitHub API URL: {url}");
                var response = await _httpClient.GetAsync(url);

                Debug.WriteLine($"GitHub response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"GitHub response content (first 100 chars): {content.Substring(0, Math.Min(100, content.Length))}...");

                    // Process differently based on whether we're checking all releases or just the latest
                    if (_includePreReleases)
                    {
                        await ProcessAllReleasesResponse(content);
                    }
                    else
                    {
                        await ProcessLatestReleaseResponse(content);
                    }
                }
                else
                {
                    Debug.WriteLine($"Failed to check for updates: {response.StatusCode}");
                    Debug.WriteLine($"Response content: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private Task ProcessAllReleasesResponse(string content)
        {
            using var doc = JsonDocument.Parse(content);
            var releases = doc.RootElement.EnumerateArray();

            if (releases.Any())
            {
                // Get the first non-draft release
                foreach (var release in releases)
                {
                    // Skip draft releases
                    if (release.TryGetProperty("draft", out var isDraftElement) &&
                        isDraftElement.GetBoolean())
                    {
                        continue;
                    }

                    string tagName = release.GetProperty("tag_name").GetString() ?? "";
                    string releaseUrl = release.GetProperty("html_url").GetString() ?? "";
                    string releaseNotes = release.GetProperty("body").GetString() ?? "";
                    bool isPrerelease = release.GetProperty("prerelease").GetBoolean();

                    Debug.WriteLine($"Found release: {tagName}, IsPrerelease: {isPrerelease}");

                    // Extract version from tag name
                    string latestVersion = NormalizeVersionFromTag(tagName);
                    Debug.WriteLine($"Normalized version: {latestVersion}");

                    if (IsNewVersionAvailable(_currentVersion, latestVersion))
                    {
                        Debug.WriteLine($"New version available: {latestVersion}");
                        UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(
                            latestVersion,
                            releaseUrl,
                            releaseNotes
                        ));
                        break;
                    }
                }

                Debug.WriteLine("No newer version found");
            }
            else
            {
                Debug.WriteLine("No releases found");
            }

            return Task.CompletedTask;
        }

        private Task ProcessLatestReleaseResponse(string content)
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Extract version tag and basic info
            string tagName = root.GetProperty("tag_name").GetString() ?? "";
            string releaseUrl = root.GetProperty("html_url").GetString() ?? "";
            string releaseNotes = root.GetProperty("body").GetString() ?? "";

            Debug.WriteLine($"Latest release tag: {tagName}");

            // Normalize version from tag
            string latestVersion = NormalizeVersionFromTag(tagName);
            Debug.WriteLine($"Normalized version: {latestVersion}");

            
            string? zipAssetUrl = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string? assetName = asset.GetProperty("name").GetString();
                    if (!string.IsNullOrEmpty(assetName) && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        zipAssetUrl = asset.GetProperty("browser_download_url").GetString();
                        Debug.WriteLine($"Found .zip asset: {zipAssetUrl}");
                        break;
                    }
                }
            }

            if (IsNewVersionAvailable(_currentVersion, latestVersion) && !string.IsNullOrEmpty(zipAssetUrl))
            {
                Debug.WriteLine($"New version available: {latestVersion}");
                UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(
                    latestVersion,
                    zipAssetUrl,    // use the real .zip binary asset URL
                    releaseNotes
                ));
            }
            else
            {
                Debug.WriteLine("Application is up to date or no valid release asset found");
            }

            return Task.CompletedTask;
        }


        private string NormalizeVersionFromTag(string tagName)
        {
            // Handle different tag formats
            if (tagName.StartsWith("v"))
            {
                return tagName.Substring(1);
            }
            else if (tagName.StartsWith("PR_v"))
            {
                return tagName.Substring(4);
            }

            // If no recognized prefix, return as is
            return tagName;
        }

        private bool IsNewVersionAvailable(string currentVersion, string latestVersion)
        {
            try
            {
                // Parse version strings (e.g., "1.2.7" -> [1, 2, 7])
                var current = ParseVersion(currentVersion);
                var latest = ParseVersion(latestVersion);

                Debug.WriteLine($"Comparing versions - Current: [{string.Join(", ", current)}], Latest: [{string.Join(", ", latest)}]");

                // Compare major version
                if (latest[0] > current[0]) return true;
                if (latest[0] < current[0]) return false;

                // Compare minor version
                if (latest[1] > current[1]) return true;
                if (latest[1] < current[1]) return false;

                // Compare patch version
                return latest[2] > current[2];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing version: {ex.Message}");
                return false;
            }
        }

        private int[] ParseVersion(string version)
        {
            var parts = version.Split('.');
            var result = new int[3] { 0, 0, 0 }; // major, minor, patch

            for (int i = 0; i < Math.Min(parts.Length, 3); i++)
            {
                if (int.TryParse(parts[i], out int value))
                {
                    result[i] = value;
                }
            }

            return result;
        }
    }

    public class UpdateAvailableEventArgs : EventArgs
    {
        public string NewVersion { get; }
        public string ReleaseUrl { get; }
        public string ReleaseNotes { get; }

        public UpdateAvailableEventArgs(string newVersion, string releaseUrl, string releaseNotes)
        {
            NewVersion = newVersion;
            ReleaseUrl = releaseUrl;
            ReleaseNotes = releaseNotes;
        }
    }
}