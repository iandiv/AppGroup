using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AppGroup.Extensions {
    public static class ExtensionCapabilities {
        public const string FolderItems = "folderItems";
        public const string TaskbarSeparator = "taskbarSeparator";
    }

    public sealed class ExtensionInfo {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("protocol")] public int Protocol { get; set; }
        [JsonPropertyName("executable")] public string Executable { get; set; } = "";
        [JsonPropertyName("capabilities")] public List<string> Capabilities { get; set; } = new();

        [JsonIgnore] public string Directory { get; set; } = "";
        [JsonIgnore] public string ExecutablePath => Path.Combine(Directory, Executable);
    }

    // Where extensions come from. Folder discovery today; a Windows App Extensions
    // (AppExtensionCatalog) source can be added for packaged/Store builds.
    public interface IExtensionSource {
        IEnumerable<ExtensionInfo> Discover();
    }

    public sealed class FolderExtensionSource : IExtensionSource {
        private const string ManifestFileName = "extension.json";

        public static IEnumerable<string> SearchRoots() {
            string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            yield return Path.Combine(exeDir, "Extensions");
            yield return Path.Combine(AppPaths.BaseDataPath, "Extensions");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGroup", "Extensions");
        }

        public IEnumerable<ExtensionInfo> Discover() {
            foreach (string root in SearchRoots().Distinct(StringComparer.OrdinalIgnoreCase)) {
                if (!System.IO.Directory.Exists(root)) continue;
                foreach (string dir in System.IO.Directory.EnumerateDirectories(root)) {
                    ExtensionInfo info = TryRead(Path.Combine(dir, ManifestFileName));
                    if (info != null) yield return info;
                }
            }
        }

        private static ExtensionInfo TryRead(string manifestPath) {
            try {
                if (!File.Exists(manifestPath)) return null;
                var info = JsonSerializer.Deserialize<ExtensionInfo>(File.ReadAllText(manifestPath));
                if (info == null || string.IsNullOrWhiteSpace(info.Id) || string.IsNullOrWhiteSpace(info.Executable))
                    return null;
                if (Path.IsPathRooted(info.Executable) || info.Executable.Contains(".."))
                    return null;
                info.Directory = Path.GetDirectoryName(manifestPath)!;
                return File.Exists(info.ExecutablePath) ? info : null;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Invalid extension manifest {manifestPath}: {ex.Message}");
                return null;
            }
        }
    }

    public static class ExtensionManager {
        public const int SupportedProtocol = 1;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
        private static readonly IExtensionSource[] Sources = { new FolderExtensionSource() };
        private static readonly object _lock = new();
        private static List<ExtensionInfo> _extensions;

        public static IReadOnlyList<ExtensionInfo> Extensions {
            get {
                lock (_lock) {
                    _extensions ??= Load();
                    return _extensions;
                }
            }
        }

        public static void Refresh() {
            lock (_lock) _extensions = null;
        }

        public static ExtensionInfo Find(string capability) =>
            Extensions.FirstOrDefault(e => e.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase));

        public static bool Has(string capability) => Find(capability) != null;

        private static List<ExtensionInfo> Load() {
            var result = new List<ExtensionInfo>();
            foreach (var source in Sources) {
                try {
                    foreach (var info in source.Discover()) {
                        if (info.Protocol != SupportedProtocol) continue;
                        if (result.Any(e => e.Id.Equals(info.Id, StringComparison.OrdinalIgnoreCase))) continue;
                        result.Add(info);
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Extension discovery failed: {ex.Message}");
                }
            }
            return result;
        }

        // Runs the extension with the given arguments and parses the single JSON object it
        // writes to stdout. Returns null if it fails, times out, or reports ok=false.
        public static async Task<JsonObject> RunAsync(string capability, params string[] args) {
            ExtensionInfo info = Find(capability);
            if (info == null) return null;

            try {
                var psi = new ProcessStartInfo {
                    FileName = info.ExecutablePath,
                    WorkingDirectory = info.Directory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                foreach (string arg in args) psi.ArgumentList.Add(arg);

                using var process = Process.Start(psi);
                if (process == null) return null;

                Task<string> readTask = process.StandardOutput.ReadToEndAsync();
                Task exitTask = process.WaitForExitAsync();
                if (await Task.WhenAny(exitTask, Task.Delay(DefaultTimeout)).ConfigureAwait(false) != exitTask) {
                    try { process.Kill(true); } catch { }
                    Debug.WriteLine($"Extension {info.Id} timed out");
                    return null;
                }

                string output = await readTask.ConfigureAwait(false);
                var json = JsonNode.Parse(output) as JsonObject;
                if (json?["ok"]?.GetValue<bool>() != true) {
                    Debug.WriteLine($"Extension {info.Id} failed: {json?["error"]}");
                    return null;
                }
                return json;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Extension {info.Id} error: {ex.Message}");
                return null;
            }
        }
    }
}
