using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;

namespace AppGroup {
    public class TbgGroupPreview {
        public StorageFolder GroupFolder { get; set; }
        public string GroupName { get; set; }
        public int ShortcutCount { get; set; }
        public int GroupCol { get; set; }
        public List<string> PathIcons { get; set; } = new List<string>();
        public int AdditionalIconsCount { get; set; }
        public string AdditionalIconsText => AdditionalIconsCount > 0 ? $"+{AdditionalIconsCount}" : string.Empty;
        public string GroupIconPath { get; set; }

    }

    public static class TbgImporter {
        public static async Task<List<TbgGroupPreview>> ScanGroupsAsync(string configPath) {
            var result = new List<TbgGroupPreview>();

            foreach (string groupPath in Directory.GetDirectories(configPath)) {
                string xmlPath = Path.Combine(groupPath, "ObjectData.xml");
                if (!File.Exists(xmlPath)) continue;

                XDocument doc = XDocument.Load(xmlPath);
                XElement cat = doc.Root;

                string groupName = cat.Element("n")?.Value?.Trim();
                if (string.IsNullOrEmpty(groupName))
                    groupName = Path.GetFileName(groupPath);

                int groupCol = 3;
                if (int.TryParse(cat.Element("Width")?.Value?.Trim(), out int parsedWidth))
                    groupCol = parsedWidth;

                var shortcuts = cat.Element("ShortcutList")?.Elements("ProgramShortcut").ToList()
                                ?? new List<XElement>();

                var iconPaths = new List<string>();
                foreach (XElement sc in shortcuts) {
                    string filePath = sc.Element("FilePath")?.Value?.Trim();
                    string shortcutName = sc.Element("n")?.Value?.Trim();
                    if (string.IsNullOrEmpty(filePath)) continue;

                    string tbgIconPath = null;
                    if (!string.IsNullOrEmpty(shortcutName)) {
                        string candidate = Path.Combine(groupPath, "Icons", shortcutName + ".png");
                        if (File.Exists(candidate)) tbgIconPath = candidate;
                    }

                    if (tbgIconPath != null) {
                        iconPaths.Add(tbgIconPath);
                    }
                    else {
                        try {
                            string cached = Path.GetExtension(filePath).Equals(".url", StringComparison.OrdinalIgnoreCase)
                                ? await IconHelper.GetUrlFileIconAsync(filePath)
                                : await IconCache.GetIconPathAsync(filePath);
                            if (!string.IsNullOrEmpty(cached) && File.Exists(cached))
                                iconPaths.Add(cached);
                        }
                        catch { }
                    }
                }

                string groupIconPath = Path.Combine(groupPath, "GroupImage.png");
                if (!File.Exists(groupIconPath)) groupIconPath = null;

                // Wrap path in StorageFolder only for GroupFolder (needed by ImportSelectedAsync)
                StorageFolder groupStorageFolder = null;
                try { groupStorageFolder = await StorageFolder.GetFolderFromPathAsync(groupPath); }
                catch { }

                int maxIcons = 7;
                result.Add(new TbgGroupPreview {
                    GroupFolder = groupStorageFolder,
                    GroupName = groupName,
                    ShortcutCount = shortcuts.Count,
                    GroupCol = groupCol,
                    PathIcons = iconPaths.Take(maxIcons).ToList(),
                    AdditionalIconsCount = Math.Max(0, iconPaths.Count - maxIcons),
                    GroupIconPath = groupIconPath
                });
            }

            return result;
        }

        public static async Task<int> ImportSelectedAsync(List<TbgGroupPreview> selected) {
            string appGroupBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGroup");
            string iconsDir = Path.Combine(appGroupBase, "Icons");
            string groupsDir = Path.Combine(appGroupBase, "Groups");
            string jsonPath = JsonConfigHelper.GetDefaultConfigPath();

            Directory.CreateDirectory(iconsDir);
            Directory.CreateDirectory(groupsDir);

            string existing = File.Exists(jsonPath)
                ? await File.ReadAllTextAsync(jsonPath)
                : "{}";
            var rootDoc = JsonDocument.Parse(existing);
            JsonObject root = JsonObject.Create(rootDoc.RootElement.Clone()) ?? new JsonObject();

            int nextId = JsonConfigHelper.GetNextGroupId();
            int imported = 0;

            foreach (var preview in selected) {
                StorageFolder groupStorageFolder = preview.GroupFolder;

                StorageFile xmlStorageFile;
                try {
                    xmlStorageFile = await groupStorageFolder.GetFileAsync("ObjectData.xml");
                }
                catch { continue; }

                XDocument doc;
                using (var stream = await xmlStorageFile.OpenStreamForReadAsync())
                    doc = XDocument.Load(stream);

                XElement cat = doc.Root;

                var pathObj = new JsonObject();
                foreach (XElement sc in cat.Element("ShortcutList")?.Elements("ProgramShortcut")
                                         ?? Array.Empty<XElement>()) {
                    string filePath = sc.Element("FilePath")?.Value?.Trim();
                    string shortcutName = sc.Element("n")?.Value?.Trim();
                    string args = sc.Element("Arguments")?.Value?.Trim();

                    if (string.IsNullOrEmpty(filePath)) continue;

                    string copiedIconPath = null;
                    try {
                        StorageFolder iconsStorageFolder = await groupStorageFolder.GetFolderAsync("Icons");
                        if (!string.IsNullOrEmpty(shortcutName)) {
                            StorageFile iconFile = await iconsStorageFolder.GetFileAsync(shortcutName + ".png");
                            string destName = Path.GetFileName(filePath) + "_tbg_" +
                                             Math.Abs(filePath.GetHashCode()).ToString("x") + ".png";
                            string destPath = Path.Combine(iconsDir, destName);
                            await iconFile.CopyAsync(
                                await StorageFolder.GetFolderFromPathAsync(iconsDir),
                                destName,
                                NameCollisionOption.ReplaceExisting);
                            copiedIconPath = destPath;
                        }
                    }
                    catch { }

                    pathObj[filePath] = new JsonObject {
                        ["tooltip"] = shortcutName,
                        ["args"] = string.IsNullOrEmpty(args) ? null : (JsonNode)args,
                        ["icon"] = copiedIconPath != null ? (JsonNode)copiedIconPath : null
                    };
                }

                string appGroupGroupDir = Path.Combine(groupsDir, preview.GroupName, preview.GroupName);
                Directory.CreateDirectory(appGroupGroupDir);

                string destIcoPath = Path.Combine(appGroupGroupDir, preview.GroupName + "_regular.ico");
                try {
                    StorageFile icoFile = await groupStorageFolder.GetFileAsync("GroupIcon.ico");
                    await icoFile.CopyAsync(
                        await StorageFolder.GetFolderFromPathAsync(appGroupGroupDir),
                        preview.GroupName + "_regular.ico",
                        NameCollisionOption.ReplaceExisting);
                }
                catch { }

                try {
                    StorageFile pngFile = await groupStorageFolder.GetFileAsync("GroupImage.png");
                    await pngFile.CopyAsync(
                        await StorageFolder.GetFolderFromPathAsync(appGroupGroupDir),
                        preview.GroupName + "_regular.png",
                        NameCollisionOption.ReplaceExisting);
                }
                catch { }

                // --- Duplicate key resolution ---
                string existingKey = root
                    .FirstOrDefault(kv =>
                        kv.Value?["groupName"]?.GetValue<string>()
                            ?.Equals(preview.GroupName, StringComparison.OrdinalIgnoreCase) == true)
                    .Key;

                string groupKey = existingKey ?? nextId.ToString();
                if (existingKey == null) nextId++;
                // ---

                root[groupKey] = new JsonObject {
                    ["groupName"] = preview.GroupName,
                    ["groupHeader"] = false,
                    ["groupCol"] = preview.GroupCol > pathObj.Count ? pathObj.Count : preview.GroupCol,
                    ["groupIcon"] = File.Exists(destIcoPath) ? destIcoPath : null,
                    ["showLabels"] = false,
                    ["labelSize"] = 12,
                    ["labelPosition"] = "Bottom",
                    ["headerPosition"] = "Bottom",
                    ["layout"] = "Default",
                    ["path"] = pathObj,
                    ["showOnTray"] = false
                };

                string shortcutPath = Path.Combine(groupsDir, preview.GroupName, $"{preview.GroupName}.lnk");
                string targetPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                    ?? Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "AppGroup.exe");

                if (File.Exists(destIcoPath)) {
                    IWshRuntimeLibrary.IWshShell wshShell = new IWshRuntimeLibrary.WshShell();
                    IWshRuntimeLibrary.IWshShortcut shortcut =
                        (IWshRuntimeLibrary.IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.Arguments = $"\"{preview.GroupName}\"";
                    shortcut.Description = $"{preview.GroupName} - AppGroup Shortcut";
                    shortcut.IconLocation = destIcoPath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                    shortcut.Save();
                }

                imported++;
            }
            if (imported > 0)
                await File.WriteAllTextAsync(jsonPath,
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            return imported;
        }
        // Add to TbgImporter
        public static List<string> FindDuplicates(List<TbgGroupPreview> selected) {
            string jsonPath = JsonConfigHelper.GetDefaultConfigPath();
            if (!File.Exists(jsonPath)) return new List<string>();

            var root = JsonDocument.Parse(File.ReadAllText(jsonPath)).RootElement;
            var existingNames = root.EnumerateObject()
                .Where(p => p.Value.TryGetProperty("groupName", out _))
                .Select(p => p.Value.GetProperty("groupName").GetString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return selected
                .Where(p => existingNames.Contains(p.GroupName))
                .Select(p => p.GroupName)
                .ToList();
        }
        public static Task<string> ResolveConfigFolderAsync(StorageFolder rootFolder) {
            var root = new DirectoryInfo(rootFolder.Path);

            // Variant 1: root/config
            DirectoryInfo config = root.GetDirectories("config", SearchOption.TopDirectoryOnly)
                                       .FirstOrDefault();

            // Variant 2: root/*/config (any single subdirectory)
            if (config == null) {
                config = root.GetDirectories("*", SearchOption.TopDirectoryOnly)
                             .Select(d => d.GetDirectories("config", SearchOption.TopDirectoryOnly)
                                           .FirstOrDefault())
                             .FirstOrDefault(d => d != null);
            }

            if (config == null)
                throw new DirectoryNotFoundException(
                    $"Could not find 'config' folder inside '{rootFolder.Path}'.");

            return Task.FromResult(config.FullName);
        }
    }
}