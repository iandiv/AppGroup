using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace AppGroup {
    public class UpdateChecker {
        private const string GITHUB_API_URL = "https://api.github.com/repos/iandiv/AppGroup/releases/latest";
        private const string RELEASES_URL = "https://github.com/iandiv/AppGroup/releases/latest";
        private static readonly HttpClient _httpClient;

        static UpdateChecker() {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AppGroup-UpdateChecker");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public class UpdateInfo {
            public bool UpdateAvailable { get; set; }
            public string CurrentVersion { get; set; } = "";
            public string LatestVersion { get; set; } = "";
            public string ReleaseUrl { get; set; } = "";
            public string ReleaseNotes { get; set; } = "";
            public string ErrorMessage { get; set; } = "";
        }

        /// <summary>
        /// Gets the current application version from the assembly
        /// </summary>
        public static string GetCurrentVersion() {
            try {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null) {
                    // Return major.minor.build format (e.g., "1.1.0")
                    return $"{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error getting current version: {ex.Message}");
            }
            return "Unknown";
        }

        /// <summary>
        /// Checks GitHub for the latest release and compares with current version
        /// </summary>
        public static async Task<UpdateInfo> CheckForUpdatesAsync() {
            var updateInfo = new UpdateInfo {
                CurrentVersion = GetCurrentVersion()
            };

            try {
                var response = await _httpClient.GetAsync(GITHUB_API_URL);

                if (!response.IsSuccessStatusCode) {
                    updateInfo.ErrorMessage = $"GitHub API returned {response.StatusCode}";
                    return updateInfo;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                // Get the tag name (version)
                if (root.TryGetProperty("tag_name", out var tagElement)) {
                    var tagName = tagElement.GetString() ?? "";
                    // Remove 'v' prefix if present (e.g., "v1.2.0" -> "1.2.0")
                    updateInfo.LatestVersion = tagName.TrimStart('v', 'V');
                }

                // Get the release URL
                if (root.TryGetProperty("html_url", out var urlElement)) {
                    updateInfo.ReleaseUrl = urlElement.GetString() ?? RELEASES_URL;
                }
                else {
                    updateInfo.ReleaseUrl = RELEASES_URL;
                }

                // Get release notes (body)
                if (root.TryGetProperty("body", out var bodyElement)) {
                    updateInfo.ReleaseNotes = bodyElement.GetString() ?? "";
                }

                // Compare versions
                updateInfo.UpdateAvailable = IsNewerVersion(updateInfo.CurrentVersion, updateInfo.LatestVersion);

                Debug.WriteLine($"Update check: Current={updateInfo.CurrentVersion}, Latest={updateInfo.LatestVersion}, UpdateAvailable={updateInfo.UpdateAvailable}");
            }
            catch (TaskCanceledException) {
                updateInfo.ErrorMessage = "Request timed out. Please check your internet connection.";
                Debug.WriteLine("Update check timed out");
            }
            catch (HttpRequestException ex) {
                updateInfo.ErrorMessage = "Unable to connect to GitHub. Please check your internet connection.";
                Debug.WriteLine($"HTTP error checking for updates: {ex.Message}");
            }
            catch (Exception ex) {
                updateInfo.ErrorMessage = $"Error checking for updates: {ex.Message}";
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
            }

            return updateInfo;
        }

        /// <summary>
        /// Compares two version strings to determine if the latest is newer
        /// </summary>
        private static bool IsNewerVersion(string current, string latest) {
            try {
                if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(latest)) {
                    return false;
                }

                // Strip pre-release suffixes (e.g., "1.2.0-beta.1" -> "1.2.0")
                var currentClean = StripPreReleaseSuffix(current);
                var latestClean = StripPreReleaseSuffix(latest);

                // Parse versions
                if (Version.TryParse(currentClean, out var currentVersion) &&
                    Version.TryParse(latestClean, out var latestVersion)) {
                    return latestVersion > currentVersion;
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error comparing versions: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Strips pre-release suffixes from version strings (e.g., "1.2.0-beta.1" -> "1.2.0")
        /// </summary>
        private static string StripPreReleaseSuffix(string version) {
            if (string.IsNullOrEmpty(version)) {
                return version;
            }

            var hyphenIndex = version.IndexOf('-');
            return hyphenIndex > 0 ? version[..hyphenIndex] : version;
        }

        /// <summary>
        /// Opens the releases page in the default browser
        /// </summary>
        public static void OpenReleasesPage(string url = "") {
            try {
                var targetUrl = string.IsNullOrEmpty(url) ? RELEASES_URL : url;
                Process.Start(new ProcessStartInfo {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error opening releases page: {ex.Message}");
            }
        }
    }
}
