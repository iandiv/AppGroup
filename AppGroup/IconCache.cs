using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppGroup {
    public static class IconCache {
        private static readonly Dictionary<string, string> _iconCache = new Dictionary<string, string>();
        private static readonly string CacheFilePath = GetCacheFilePath();
        private static readonly object _cacheLock = new object();

        // Per-file semaphores — concurrent callers for the same source file
        // wait for the first extraction rather than all extracting in parallel.
        private static readonly Dictionary<string, SemaphoreSlim> _extractionLocks
            = new Dictionary<string, SemaphoreSlim>();
        private static readonly object _extractionLocksLock = new object();

        static IconCache() {
            LoadIconCache();
        }

        // ── private helpers ───────────────────────────────────────────────────

        private static string GetCacheFilePath() {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appGroupFolder = Path.Combine(folder, "AppGroup");
            Directory.CreateDirectory(appGroupFolder);
            return Path.Combine(appGroupFolder, "icon_cache.json");
        }

        private static void LoadIconCache() {
            try {
                if (!File.Exists(CacheFilePath)) return;
                string json = File.ReadAllText(CacheFilePath);
                var cacheData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (cacheData == null) return;
                lock (_cacheLock) {
                    _iconCache.Clear();
                    foreach (var kvp in cacheData) {
                        // Only keep entries whose PNG still exists on disk
                        if (!string.IsNullOrEmpty(kvp.Key) &&
                            !string.IsNullOrEmpty(kvp.Value) &&
                            File.Exists(kvp.Value))
                            _iconCache[kvp.Key] = kvp.Value;
                    }
                }
                Debug.WriteLine($"IconCache loaded {_iconCache.Count} valid entries.");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to load icon cache: {ex.Message}");
            }
        }

        private static SemaphoreSlim GetExtractionSemaphore(string filePath) {
            lock (_extractionLocksLock) {
                if (!_extractionLocks.TryGetValue(filePath, out var sem))
                    _extractionLocks[filePath] = sem = new SemaphoreSlim(1, 1);
                return sem;
            }
        }

        // ── public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a stable cache key encoding path + last-write-time + size.
        /// Returns empty string if the file does not exist.
        /// </summary>
        public static string ComputeFileCacheKey(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return string.Empty;
            var fi = new FileInfo(filePath);
            // Use pipe separator — paths can contain underscores
            return $"{filePath}|{fi.LastWriteTimeUtc.Ticks}|{fi.Length}";
        }
        /// <summary>
        /// Returns a cached PNG path for a folder, extracting the shell folder icon if needed.
        /// Separate from GetIconPathAsync because directories fail File.Exists checks.
        /// </summary>
        public static async Task<string> GetFolderIconPathAsync(string folderPath) {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return null;

            // Build a stable cache key from path + last-write time
            string cacheKey;
            try {
                var di = new DirectoryInfo(folderPath);
                cacheKey = $"{folderPath}|{di.LastWriteTimeUtc.Ticks}";
            }
            catch { return null; }

            // Fast path
            lock (_cacheLock) {
                if (_iconCache.TryGetValue(cacheKey, out var cached) &&
                    !string.IsNullOrEmpty(cached) && File.Exists(cached))
                    return cached;
                _iconCache.Remove(cacheKey);
            }

            var sem = GetExtractionSemaphore(folderPath);
            await sem.WaitAsync().ConfigureAwait(false);
            try {
                // Re-check after acquiring semaphore
                lock (_cacheLock) {
                    if (_iconCache.TryGetValue(cacheKey, out var cached) &&
                        !string.IsNullOrEmpty(cached) && File.Exists(cached))
                        return cached;
                }

                string outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AppGroup", "Icons");
                Directory.CreateDirectory(outputDir);

                // Pass folderPath directly — SHGetFileInfo handles directories
                string extracted = await IconHelper.ExtractFolderIconAndSaveAsync(
     folderPath, outputDir, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(extracted) && File.Exists(extracted)) {
                    lock (_cacheLock) {
                        _iconCache[cacheKey] = extracted;
                    }
                    SaveIconCache();
                    return extracted;
                }

                return null;
            }
            finally {
                sem.Release();
            }
        }
        /// <summary>
        /// Returns the cached PNG path for a source file, extracting it first if needed.
        /// Re-extracts only when the cached PNG no longer exists on disk.
        /// </summary>
        public static async Task<string> GetIconPathAsync(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            string cacheKey = ComputeFileCacheKey(filePath);
            if (string.IsNullOrEmpty(cacheKey)) return null;

            // Fast path — valid cached PNG exists on disk
            lock (_cacheLock) {
                if (_iconCache.TryGetValue(cacheKey, out var cached) &&
                    !string.IsNullOrEmpty(cached) && File.Exists(cached))
                    return cached;
                // Stale pointer — remove it so extraction runs cleanly
                _iconCache.Remove(cacheKey);
            }

            // Serialise extraction per source file so concurrent callers don't
            // each extract and then race to store the result.
            var sem = GetExtractionSemaphore(filePath);
            await sem.WaitAsync().ConfigureAwait(false);
            try {
                // Re-check: another waiter may have populated the entry
                lock (_cacheLock) {
                    if (_iconCache.TryGetValue(cacheKey, out var cached) &&
                        !string.IsNullOrEmpty(cached) && File.Exists(cached))
                        return cached;
                    _iconCache.Remove(cacheKey);
                }

                string outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AppGroup", "Icons");
                Directory.CreateDirectory(outputDir);

                string extracted = await IconHelper.ExtractIconAndSaveAsync(
                    filePath, outputDir, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(extracted) && File.Exists(extracted)) {
                    lock (_cacheLock) {
                        _iconCache[cacheKey] = extracted;
                    }
                    SaveIconCache();
                    Debug.WriteLine($"IconCache: extracted {filePath} -> {extracted}");
                    return extracted;
                }

                return null;
            }
            finally {
                sem.Release();
            }
        }

        /// <summary>
        /// Forces the next GetIconPathAsync call for this file to re-extract,
        /// even if the source file itself hasn't changed on disk.
        /// Call this after a custom icon is assigned to an item in the editor.
        /// </summary>
        public static void InvalidateEntry(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return;
            string cacheKey = ComputeFileCacheKey(filePath);
            if (string.IsNullOrEmpty(cacheKey)) return;
            bool removed;
            lock (_cacheLock) {
                removed = _iconCache.Remove(cacheKey);
            }
            if (removed) {
                SaveIconCache();
                Debug.WriteLine($"IconCache: invalidated {filePath}");
            }
        }

        /// <summary>
        /// Stores an already-resolved PNG path under a source file's cache key.
        /// Use when you have a PNG from outside the normal extraction flow
        /// (e.g. GetLnkIconAsync resolved the exe target and already saved the PNG).
        /// </summary>
        public static void StoreEntry(string filePath, string pngPath) {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(pngPath)) return;
            string cacheKey = ComputeFileCacheKey(filePath);
            if (string.IsNullOrEmpty(cacheKey)) return;
            lock (_cacheLock) {
                _iconCache[cacheKey] = pngPath;
            }
            SaveIconCache();
        }

        /// <summary>
        /// Thread-safe lookup without triggering extraction.
        /// Returns true and sets iconPath when a valid on-disk entry exists.
        /// Takes filePath (not a raw key) so callers don't need to call ComputeFileCacheKey.
        /// </summary>
        public static bool TryGetCachedPath(string filePath, out string iconPath) {
            iconPath = null;
            if (string.IsNullOrEmpty(filePath)) return false;
            string cacheKey = ComputeFileCacheKey(filePath);
            if (string.IsNullOrEmpty(cacheKey)) return false;
            lock (_cacheLock) {
                if (_iconCache.TryGetValue(cacheKey, out var cached) &&
                    !string.IsNullOrEmpty(cached) && File.Exists(cached)) {
                    iconPath = cached;
                    return true;
                }
            }
            return false;
        }

        public static void SaveIconCache() {
            try {
                Dictionary<string, string> snapshot;
                lock (_cacheLock) {
                    snapshot = new Dictionary<string, string>(_iconCache);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFilePath, json);
                Debug.WriteLine($"IconCache saved {snapshot.Count} entries.");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to save icon cache: {ex.Message}");
            }
        }

        public static async Task<BitmapImage> LoadImageFromPathAsync(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            try {
                var bitmapImage = new BitmapImage();
                using var stream = File.OpenRead(filePath);
                using var randomAccessStream = stream.AsRandomAccessStream();
                await bitmapImage.SetSourceAsync(randomAccessStream);
                return bitmapImage;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to load image: {ex.Message}");
                return null;
            }
        }
    }
}