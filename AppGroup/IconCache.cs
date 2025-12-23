
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
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
                        foreach (var kvp in cacheData) {
                            if (!string.IsNullOrEmpty(kvp.Value) && File.Exists(kvp.Value)) {
                                _iconCache[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    Debug.WriteLine($"Cache loaded from {CacheFilePath}");
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
        public static async Task<string> GetIconPathAsync(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            string cacheKey = ComputeFileCacheKey(filePath);

            if (_iconCache.TryGetValue(cacheKey, out var cachedIconPath)) {
                return cachedIconPath;
            }

            try {
                string outputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AppGroup",
                    "Icons"
                );
                Directory.CreateDirectory(outputDirectory);

                var extractedIconPath = await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

                if (extractedIconPath != null && File.Exists(extractedIconPath)) {
                    _iconCache[cacheKey] = extractedIconPath;
                    SaveIconCache();
                    return extractedIconPath;
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Icon extraction failed for {filePath}: {ex.Message}");
            }

            return null;
        }
        public static void SaveIconCache() {
            try {
                string json = JsonSerializer.Serialize(_iconCache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFilePath, json);
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
