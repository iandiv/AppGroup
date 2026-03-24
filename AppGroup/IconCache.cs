
using IWshRuntimeLibrary;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using File = System.IO.File;
namespace AppGroup
{

    public static class IconCache {
        public static  Dictionary<string, string> _iconCache = new Dictionary<string, string>();
        private static readonly string CacheFilePath = GetCacheFilePath();

        static IconCache() {
            LoadIconCache();
        }

        private static string GetCacheFilePath() {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appGroupFolder = Path.Combine(folder, "AppGroup");
            Directory.CreateDirectory(appGroupFolder); 
            return Path.Combine(appGroupFolder, "icon_cache.json");
        }

        private static void LoadIconCache() {
            try {
                if (File.Exists(CacheFilePath)) {
                    string json = File.ReadAllText(CacheFilePath);
                    var cacheData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                    if (cacheData != null) {
                        bool anyPruned = false;
                        foreach (var kvp in cacheData) {
                            if (!string.IsNullOrEmpty(kvp.Value) && File.Exists(kvp.Value)) {
                                _iconCache[kvp.Key] = kvp.Value;
                            }
                            else {
                                anyPruned = true; // stale entry detected
                            }
                        }
                        if (anyPruned) {
                            SaveIconCache(); // flush pruned entries so JSON stays in sync
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to load cache: {ex.Message}");
            }
        }

        public static async Task<BitmapImage> LoadImageFromPathAsync(string filePath) {
            BitmapImage bitmapImage = new BitmapImage();

            try {
                using var stream = File.OpenRead(filePath);
                using var randomAccessStream = stream.AsRandomAccessStream();
                await bitmapImage.SetSourceAsync(randomAccessStream);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to load image: {ex.Message}");
            }

            return bitmapImage;
        }
        //public static async Task<string> GetIconPathAsync(string filePath) {
        //    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

        //    string cacheKey = ComputeFileCacheKey(filePath);

        //    if (_iconCache.TryGetValue(cacheKey, out var cachedIconPath)) {
        //        if (File.Exists(cachedIconPath))
        //            return cachedIconPath;

        //        _iconCache.Remove(cacheKey);  // evict dead entry
        //        SaveIconCache();              // persist removal immediately
        //    }

        //    try {
        //        string outputDirectory = Path.Combine(
        //            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        //            "AppGroup", "Icons"
        //        );
        //        Directory.CreateDirectory(outputDirectory);

        //        var extractedIconPath = await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

        //        if (extractedIconPath != null && File.Exists(extractedIconPath)) {
        //            _iconCache[cacheKey] = extractedIconPath;
        //            SaveIconCache();
        //            return extractedIconPath;
        //        }
        //    }
        //    catch (Exception ex) {
        //        Debug.WriteLine($"Icon extraction failed for {filePath}: {ex.Message}");
        //    }

        //    return null;
        //}
        public static async Task<string> GetIconPathAsync(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            string cacheKey = ComputeFileCacheKey(filePath);

            if (_iconCache.TryGetValue(cacheKey, out var cachedIconPath)) {
                if (File.Exists(cachedIconPath)) {
                    // Evict stale _shell or hash-suffixed entries from old naming convention
                    if (cachedIconPath.Contains("_shell") ||
                        System.Text.RegularExpressions.Regex.IsMatch(
                            Path.GetFileNameWithoutExtension(cachedIconPath), @"_[a-f0-9]{16}$")) {
                        _iconCache.Remove(cacheKey);
                        SaveIconCache();
                        // fall through to re-extract
                    }
                    else {
                        return cachedIconPath;
                    }
                }
                else {
                    _iconCache.Remove(cacheKey);
                    SaveIconCache();
                }
            }

            string outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppGroup", "Icons"
            );
            Directory.CreateDirectory(outputDirectory);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string extractedPath = ext is ".lnk" or ".msc" or ".cpl"
                ? await IconHelper.GetLnkIconAsync(filePath)
                : await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

            if (extractedPath != null && File.Exists(extractedPath)) {
                _iconCache[cacheKey] = extractedPath;
                SaveIconCache();
                return extractedPath;
            }

            return null;
        }
        //        public static async Task<string> GetIconPathAsync(string filePath) {
        //    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

        //    string cacheKey = ComputeFileCacheKey(filePath);
        //    if (_iconCache.TryGetValue(cacheKey, out var cachedIconPath)) {
        //        if (File.Exists(cachedIconPath)) return cachedIconPath;
        //        _iconCache.Remove(cacheKey);
        //        SaveIconCache();
        //    }

        //    string outputDirectory = Path.Combine(
        //        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        //        "AppGroup", "Icons"
        //    );
        //    Directory.CreateDirectory(outputDirectory);

        //    string ext = Path.GetExtension(filePath).ToLowerInvariant();
        //    string extractedPath = ext is ".lnk" or ".msc" or ".cpl"
        //        ? await IconHelper.GetLnkIconAsync(filePath)
        //        : await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

        //    if (extractedPath != null && File.Exists(extractedPath)) {
        //        _iconCache[cacheKey] = extractedPath;
        //        SaveIconCache();
        //        return extractedPath;
        //    }
        //    return null;
        //}
        //public static async Task<string> GetIconPathAsync(string filePath) {
        //    Debug.WriteLine($"GetIconPathAsync thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}, IsBackground: {System.Threading.Thread.CurrentThread.IsBackground}, IsThreadPoolThread: {System.Threading.Thread.CurrentThread.IsThreadPoolThread}");

        //    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

        //    string cacheKey = ComputeFileCacheKey(filePath);
        //    if (_iconCache.TryGetValue(cacheKey, out var cachedIconPath)) {
        //        if (File.Exists(cachedIconPath)) return cachedIconPath;
        //        _iconCache.Remove(cacheKey);
        //        SaveIconCache();
        //    }

        //    // Warm up shell HERE so it applies regardless of caller thread
        //    var shfi = new NativeMethods.SHFILEINFO();
        //    NativeMethods.SHGetFileInfo(
        //        filePath, 0, ref shfi,
        //        (uint)Marshal.SizeOf(shfi),
        //        NativeMethods.SHGFI_SYSICONINDEX
        //    );
        //    Guid iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        //    NativeMethods.SHGetImageList(NativeMethods.SHIL_JUMBO, ref iid, out NativeMethods.IImageList imageList);
        //    IntPtr hIcon = IntPtr.Zero;
        //    imageList?.GetIcon(shfi.iIcon, 1, ref hIcon);
        //    if (hIcon != IntPtr.Zero) NativeMethods.DestroyIcon(hIcon);
        //    await Task.Delay(50);

        //    string outputDirectory = Path.Combine(
        //        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        //        "AppGroup", "Icons"
        //    );
        //    Directory.CreateDirectory(outputDirectory);

        //    string ext = Path.GetExtension(filePath).ToLowerInvariant();
        //    string extractedPath = ext is ".lnk" or ".msc" or ".cpl"
        //        ? await IconHelper.GetLnkIconAsync(filePath)
        //        : await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

        //    if (extractedPath != null && File.Exists(extractedPath)) {
        //        _iconCache[cacheKey] = extractedPath;
        //        SaveIconCache();
        //        return extractedPath;
        //    }
        //    return null;
        //}


        //public static async Task<string> GetIconPathAsync(string filePath) {
        //    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

        //    string cacheKey = ComputeFileCacheKey(filePath);
        //    if (_iconCache.TryGetValue(cacheKey, out var cachedIconPath)) {
        //        if (File.Exists(cachedIconPath)) return cachedIconPath;
        //        _iconCache.Remove(cacheKey);
        //        SaveIconCache();
        //    }

        //    string outputDirectory = Path.Combine(
        //             Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        //             "AppGroup", "Icons"
        //         );
        //    Directory.CreateDirectory(outputDirectory);

        //    // Use SHGetFileInfo for .lnk, .msc, .cpl — shell resolves everything
        //    string ext = Path.GetExtension(filePath).ToLowerInvariant();
        //    string extractedPath = ext is ".lnk" or ".msc" or ".cpl"
        //        ? await IconHelper.GetLnkIconAsync(filePath)
        //        : await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

        //    if (extractedPath != null && File.Exists(extractedPath)) {
        //        _iconCache[cacheKey] = extractedPath;
        //        SaveIconCache();
        //        return extractedPath;
        //    }
        //    return null;
        //}

        public static void SaveIconCache() {
            try {
                string json = JsonSerializer.Serialize(_iconCache, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(CacheFilePath, json);
                Debug.WriteLine($"Cache saved to {CacheFilePath}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to save cache: {ex.Message}");
            }
        }

        public static string ComputeFileCacheKey(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return filePath ?? string.Empty;
            }
            var fileInfo = new FileInfo(filePath);
            return $"{filePath}_{fileInfo.LastWriteTimeUtc}_{fileInfo.Length}";
        }
    }

}
